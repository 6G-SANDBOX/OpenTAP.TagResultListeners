// Author:      Bruno Garcia Garcia <bgarcia@lcc.uma.es>
// Copyright:   Copyright 2019-2020 Universidad de Málaga (University of Málaga), Spain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.IO;
using OpenTap;

using InfluxDB.Client;
using System.Security;
using System.Globalization;

using OpenTap.InfluxDb.Extensions;
using InfluxDB.Client.Writes;

namespace OpenTap.InfluxDb.ResultListeners
{
    [Display("InfluxDB", Group: "UMA", Description: "InfluxDB result listener")]
    public class InfluxDbResultListener : ConfigurableResultListenerBase
    {
        private InfluxDBClient client = null;
        private DateTime startTime;
        private Dictionary<string, string> baseTags = null;
        private bool executionIdWarning = false;

        #region Settings

        [Display("Address", Group: "InfluxDB", Order: 1.0)]
        public string Address { get; set; }

        [Display("Port", Group: "InfluxDB", Order: 1.1)]
        public int Port { get; set; }

        [Display("Bucket", Group: "InfluxDB", Order: 1.2)]
        public string Bucket { get; set; }

        [Display("Org", Group: "InfluxDB", Order: 1.3)]
        public string Org { get; set; }

        [Display("Token", Group: "InfluxDB", Order: 1.4)]
        public SecureString Token { get; set; }

        [Display("Save log messages", Group: "InfluxDB", Order: 1.5, 
            Description: "Send TAP log messages to InfluxDB after testplan execution.")]
        public bool HandleLog { get; set; }

        [Display("Log levels", Group: "InfluxDB", Order: 1.6, 
            Description: "Filter sent messages by severity level.")]
        [EnabledIf("HandleLog", true)]
        public LogLevel LogLevels { get; set; }

        [Display("Facility", Group: "Tags", Order: 2.0)]
        public string Facility { get; set; }

        [Display("Host IP", Group: "Tags", Order: 2.1)]
        public string HostIP { get; set; }

        [Display("DateTime overrides", Group: "Result Timestamps", Order: 4.0,
            Description: "Allows the use of certain result columns to be parsed for generating\n" +
                         "the row timestamp. Assumes that the result uses the Local timestamp\n" + 
                         "instead of UTC.")]
        public List<DateTimeOverride> Overrides { get; set; }

        #endregion

        public InfluxDbResultListener()
        {
            Name = "INFLUX2";

            Address = "localhost";
            Port = 8086;
            Bucket = "mybucket";
            HandleLog = false;
            Org =  Facility = HostIP = string.Empty;
            Token = new SecureString();
            LogLevels = LogLevel.Info | LogLevel.Warning | LogLevel.Error;
            SetExecutionId = false;
            Overrides = new List<DateTimeOverride>();
        }

        public override void Open()
        {
            base.Open();
            this.client = new InfluxDBClient($"http://{Address}:{Port}?org={Org}&bucket={Bucket}&token={Token.GetString()}");
        }

        public override void Close()
        {
            base.Close();
            this.client = null;
        }

        public override void OnTestPlanRunStart(TestPlanRun planRun)
        {
            base.OnTestPlanRunStart(planRun);

            this.startTime = planRun.StartTime.ToUniversalTime();
            this.baseTags = new Dictionary<string, string> {
                { "appname", $"TAP ({PluginManager.GetOpenTapAssembly().SemanticVersion.ToString()})" },
                { "facility", Facility },
                { "host", HostIP },
                { "hostname", EngineSettings.Current.StationName }
            };

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

            int ignored = 0, count = 0;
            string sanitizedName = Sanitize(result.Name, "_");

            using (WriteApi writer = client.GetWriteApi()) {
                writer.EventHandler += writerEventHandler;

                DateTimeOverride timestampParser = Overrides.Where((over) => (over.ResultName == result.Name)).FirstOrDefault();

                PointData builder = PointData.Measurement(sanitizedName);
                foreach (KeyValuePair<string, string> tag in this.getTags()) {
                    builder = builder.Tag(tag.Key, tag.Value);
                }

                List<PointData> points = new List<PointData>();

                foreach (Dictionary<string, IConvertible> row in getRows(result))
                {
                    DateTime? maybeDatetime = timestampParser != null ? timestampParser.Parse(row) : getDateTime(row);
                    if (maybeDatetime.HasValue)
                    {
                        PointData point = builder.Timestamp(maybeDatetime.Value, InfluxDB.Client.Api.Domain.WritePrecision.Ms);

                        foreach (KeyValuePair<string, IConvertible> item in row)
                        {
                            if (item.Value != null) // Null (unsent) values will appear as empty columns
                            {
                                // Avoid sending invalid values to the database
                                if (!(item.Value.ToString() == "9.91E+37" || item.Value.ToString() == "Infinity"))
                                {
                                    point = point.Field(item.Key, item.Value);
                                }
                            }
                        }
                        points.Add(point);

                        count++;
                    }
                    else { ignored++; }
                }

                if (ignored != 0) { Log.Warning($"Ignored {ignored}/{result.Rows} results from table {result.Name}: Could not parse Timestamp"); }

                Log.Info($"Sending {count} results ('{result.Name}'{(sanitizedName != result.Name ? $" as '{sanitizedName}'" : "")}) to {Name}");
                writer.WritePoints(points);
            }

            OnActivity();
        }

        private void writerEventHandler(object sender, EventArgs e)
        {
            string message = null;
            string point = null;
            switch (e)
            {
                // success response from server
                case WriteSuccessEvent _:
                    break;
                case WriteErrorEvent error:
                    message = $"WriteError: {error.Exception.Message}";
                    point = error.LineProtocol;
                    break;
                case WriteRetriableErrorEvent error:
                    message = $"WriteRetriableError: {error.Exception.Message}";
                    point = error.LineProtocol;
                    break;
                case WriteRuntimeExceptionEvent error:
                    message = $"WriteRuntimeException: {error.Exception.Message}";
                    break;
            }

            if (message != null) {
                Log.Error(message);
                if (point != null) {
                    Log.Debug($"Point data: {point}");
                }
            }
        }

        private Dictionary<string, string> getTags(params string[] extra)
        {
            if (extra.Length % 2 != 0) { throw new ArgumentException("Odd number of tokens."); }

            if (extra.Length == 0 && !SetExecutionId) { return this.baseTags; }
            else
            {
                Dictionary<string, string> res = new Dictionary<string, string>(this.baseTags);
                for (int i = 0; i < extra.Length; i += 2)
                {
                    res.Add(extra[i], extra[i + 1]);
                }
                if (SetExecutionId && !string.IsNullOrEmpty(ExecutionId))
                {
                    res.Add("ExecutionId", ExecutionId);
                }
                return res;
            }
        }
    }
}
