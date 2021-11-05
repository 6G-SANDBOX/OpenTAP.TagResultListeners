// Author:      Bruno Garcia Garcia <bgarcia@lcc.uma.es>
// Copyright:   Universidad de Málaga (University of Málaga), Spain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using RestSharp;

namespace OpenTap.Plugins.UMA.ResultListeners {
    [Display( "MQTTPublisher", Group: "UMA", Description: "MQTT publisher result listener" )]
    public class MqttPublisherResultListener : ConfigurableResultListenerBase {
        private bool executionIdWarning = false;

        private RestClient client = null;

        #region Settings

        [Display( "Address", Group: "Publisher", Order: 1.0 )]
        public string Address { get; set; }

        [Display( "Port", Group: "Publisher", Order: 1.1 )]
        public int Port { get; set; }

        [Display( "HTTPS", Group: "Publisher", Order: 1.2 )]
        public bool Https { get; set; }

        [Display( "MQTT metadata overrides", Group: "Publisher", Order: 1.3,
            Description: "Metadata assigned to each kind of measurement. Results that do not appear\n" +
                         "in this table will not be sent to the MQTT Publisher." )]
        public List<MqttPublisherOverride> MqttOverrides { get; set; }

        [Display( "DateTime overrides", Group: "Result Timestamps", Order: 2.0,
            Description: "Allows the use of certain result columns to be parsed for generating\n" +
                         "the row timestamp. Assumes that the result uses the Local timestamp\n" +
                         "instead of UTC." )]
        public List<DateTimeOverride> DateOverrides { get; set; }

        #endregion

        #region Metadata

        [XmlIgnore]
        public string UseCase { get; set; }

        [XmlIgnore]
        public string TestbedId { get; set; }

        [XmlIgnore]
        public string ScenarioId { get; set; }

        [XmlIgnore]
        public string NetAppId { get; set; }

        #endregion

        public MqttPublisherResultListener( ) {
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

        public override void Open( ) {
            base.Open();
            this.client = new RestClient( $"http{( Https ? "s" : "" )}://{Address}:{Port}/" );
            this.UseCase = this.TestbedId = this.ScenarioId = this.NetAppId = string.Empty;
        }

        public override void Close( ) {
            this.client = null;
            base.Close();
        }

        public override void OnTestPlanRunStart( TestPlanRun planRun ) {
            base.OnTestPlanRunStart( planRun );

            executionIdWarning = false;
        }

        public override void OnResultPublished( Guid stepRun, ResultTable result ) {
            result = ProcessResult( result );
            if ( result == null ) { return; }

            if ( string.IsNullOrWhiteSpace( ExecutionId ) ) {
                if ( !executionIdWarning ) {
                    Log.Error( $"{Name}: Results published before setting Execution Id. No results will be handled." );
                    executionIdWarning = true;
                }

                return;
            } else {
                Dictionary<string, object> payload = new Dictionary<string, object>();
                payload["category"] = "experiment";

                var payloadData = getPayloadData( result );
                if ( payloadData.Count != 0 ) {
                    payload["data"] = payloadData;
                    payload["experiment_id"] = ExecutionId;
                    payload["use_case_id"] = this.UseCase;
                    payload["testbed_id"] = this.TestbedId;
                    payload["scenario_id"] = this.ScenarioId;
                    payload["netapp_id"] = this.NetAppId;

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
                         *  - "origin" and "unit" are sent only if defined in MqttOverrides
                         */

                        if ( item.Value != null ) // Do not send empty keys
                        {
                            MqttPublisherOverride mqttData = MqttOverrides.Where(
                                ( over ) => ( over.ResultName == result.Name && over.Column == item.Key ) ).FirstOrDefault();

                            if ( mqttData != null ) {
                                Dictionary<string, object> single = new Dictionary<string, object>();
                                single["type"] = string.IsNullOrWhiteSpace( mqttData.Type ) ? item.Key : mqttData.Type;
                                if ( !string.IsNullOrWhiteSpace( mqttData.Unit ) ) { single["unit"] = mqttData.Unit; }
                                if ( !string.IsNullOrWhiteSpace( mqttData.Origin ) ) { single["origin"] = mqttData.Origin; }
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
