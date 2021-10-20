// Author:      Bruno Garcia Garcia <bgarcia@lcc.uma.es>
// Copyright:   Universidad de Málaga (University of Málaga), Spain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.IO;
using OpenTap;

using InfluxDB.LineProtocol.Client;
using InfluxDB.LineProtocol.Payload;
using System.Security;
using System.Globalization;

using OpenTap.Plugins.UMA.Extensions;

using RestSharp;
using RestSharp.Extensions;

namespace OpenTap.Plugins.UMA.ResultListeners
{
    [Display("MQTTPublisher", Group: "UMA", Description: "MQTT publisher result listener")]
    public class MqttPublisherResultListener : ConfigurableResultListenerBase
    {
        private bool executionIdWarning = false;

        #region Settings

        [Display("Address", Group: "Publisher", Order: 1.0)]
        public string Address { get; set; }

        [Display("Port", Group: "Publisher", Order: 1.1)]
        public int Port { get; set; }

        [Display( "HTTPS", Group: "Publisher", Order: 1.2 )]
        public bool Https { get; set; }

        [Display("DateTime overrides", Group: "Result Timestamps", Order: 4.0,
            Description: "Allows the use of certain result columns to be parsed for generating\n" +
                         "the row timestamp. Assumes that the result uses the Local timestamp\n" +
                         "instead of UTC.")]
        public List<DateTimeOverride> DateOverrides { get; set; }

        public List<MqttPublisherOverride> MqttOverrides { get; set; }

        #endregion

        RestClient client = null;

        public MqttPublisherResultListener( )
        {
            Name = "PUBL";

            Address = "localhost";
            Port = 5000;
            Https = false;

            SetExecutionId = false;
            DateOverrides = new List<DateTimeOverride>();
            MqttOverrides = new List<MqttPublisherOverride>();

            Rules.Add( ( ) => ( !string.IsNullOrWhiteSpace( Address ) ), "Please select an address", "Address" );
            Rules.Add( ( ) => ( Port > 0 ), "Please select a valid port number", "Port" );
        }

        public override void Open()
        {
            base.Open();
            this.client = new RestClient( $"http{( Https ? "s" : "" )}://{Address}:{Port}/" );
        }

        public override void Close()
        {
            this.client = null;
            base.Close();
        }

        public override void OnTestPlanRunStart(TestPlanRun planRun)
        {
            base.OnTestPlanRunStart(planRun);

            executionIdWarning = false;
        }

        public override void OnResultPublished(Guid stepRun, ResultTable result)
        {
            result = ProcessResult(result);
            if (result == null) { return; }

            if (SetExecutionId && !executionIdWarning && string.IsNullOrEmpty(ExecutionId))
            {
                Log.Warning($"{Name}: Results published before setting Execution Id");
                executionIdWarning = true;
            }

            string sanitizedName = Sanitize(result.Name, "_");

            Dictionary<string, object> payload = new Dictionary<string, object>();
            payload["category"] = "experiment";

            var payloadData = getPayloadData( result );
            if ( payloadData.Count != 0 ) {
                payload["data"] = payloadData;

                RestRequest request = new RestRequest( "publish", Method.POST, DataFormat.Json );
                request.AddJsonBody( payload );

                IRestResponse<MqttPublisherReply> response = client.Execute<MqttPublisherReply>( request, Method.POST );
                if ( !response.IsSuccessful ) {
                    string message = response.Data != null ? response.Data.Message : response.ErrorMessage;
                    Log.Error( $"Exception while connecting with Publisher ({response.StatusCode}): {message}" );
                }
            } else {
                Log.Warning( $"Could not retrieve any publishable results from {result.Name} table." );
            }

            OnActivity();
        }

        private List<Dictionary<string, object>> getPayloadData( ResultTable result ) {
            DateTimeOverride timestampParser = DateOverrides.Where( ( over ) => ( over.ResultName == result.Name ) ).FirstOrDefault();

            List<Dictionary<string, object>> data = new List<Dictionary<string, object>>();

            foreach ( Dictionary<string, IConvertible> row in getRows( result ) ) {
                DateTime? maybeDatetime = timestampParser != null ? timestampParser.Parse( row ) : getDateTime( row );
                if ( maybeDatetime.HasValue ) {
                    foreach ( KeyValuePair<string, IConvertible> item in row ) {
                        /* 
                         * The specific details of what to send in corner cases should be agreed with the Analytics team. Right now:
                         *  - NaN and Infinity are sent unchecked, see the InfluxDb result listener for possible checks against this.
                         *  - All the results (from all the rows) in the ResultTable are sent in the same payload, with one item per 
                         *  cell in the table.
                         *  - Empty values (cells) are not sent at all.
                         *  - Values not configured in MqttOverrides are ignored.
                         */

                        if ( item.Value != null ) // Do not send empty keys
                        {
                            MqttPublisherOverride mqttData = MqttOverrides.Where(
                                ( over ) => ( over.ResultName == result.Name && over.Column == item.Key ) ).FirstOrDefault();

                            if ( mqttData != null ) {
                                Dictionary<string, object> single = new Dictionary<string, object>();
                                single["type"] = string.IsNullOrWhiteSpace(mqttData.Type) ? item.Key : mqttData.Type;
                                single["unit"] = mqttData.Unit;
                                single["origin"] = mqttData.Origin;
                                single["timestamp"] = maybeDatetime.Value;
                                single["value"] = item.Value;

                                data.Add( single );
                            }
                        }
                    }
                }
            }

            return data;
        }
    }
}
