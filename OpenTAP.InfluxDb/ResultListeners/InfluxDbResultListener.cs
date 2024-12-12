// Author:      Bruno Garcia Garcia <bgarcia@lcc.uma.es>
// Copyright:   Copyright 2019-2020 Universidad de Málaga (University of Málaga), Spain

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

using OpenTap.InfluxDb.Extensions;

namespace OpenTap.InfluxDb.ResultListeners
{
    [Display("InfluxDB", Group: "UMA", Description: "InfluxDB result listener")]
    public class InfluxDbResultListener : ConfigurableResultListenerBase
    {
        private LineProtocolClient client = null;
        private DateTime startTime;
        private Dictionary<string, string> baseTags = null;
        private bool executionIdWarning = false;

        #region Settings

        [Display("Address", Group: "InfluxDB", Order: 1.0)]
        public string Address { get; set; }

        [Display("Port", Group: "InfluxDB", Order: 1.1)]
        public int Port { get; set; }

        [Display("Database", Group: "InfluxDB", Order: 1.2)]
        public string Database { get; set; }

        [Display("User", Group: "InfluxDB", Order: 1.3)]
        public string User { get; set; }

        [Display("Password", Group: "InfluxDB", Order: 1.4)]
        public SecureString Password { get; set; }

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
            Name = "INFLUX";

            Address = "localhost";
            Port = 8086;
            Database = "mydb";
            HandleLog = true;
            User =  Facility = HostIP = string.Empty;
            Password = new SecureString();
            LogLevels = LogLevel.Info | LogLevel.Warning | LogLevel.Error;
            SetExecutionId = false;
            Overrides = new List<DateTimeOverride>();
        }

        public override void Open()
        {
            base.Open();
            this.client = new LineProtocolClient(new Uri($"http://{Address}:{Port}"), Database, User, Password.GetString());
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

            LineProtocolPayload payload = new LineProtocolPayload();
            int ignored = 0, count = 0;
            string sanitizedName = Sanitize(result.Name, "_");

            DateTimeOverride timestampParser = Overrides.Where((over) => (over.ResultName == result.Name)).FirstOrDefault();
            
            foreach (Dictionary<string, IConvertible> row in getRows(result))
            {
                DateTime? maybeDatetime = timestampParser != null ? timestampParser.Parse(row) : getDateTime(row);
                if (maybeDatetime.HasValue)
                {
                    Dictionary<string, object> fields = new Dictionary<string, object>();
                    foreach (KeyValuePair<string, IConvertible> item in row)
                    {
                        if (item.Value != null) // Null (unsent) values will appear as empty columns
                        {
                            // Avoid sending invalid values to the database
                            if (!(item.Value.ToString() == "9.91E+37" || item.Value.ToString() == "Infinity")) 
                            {
                                fields[item.Key] = item.Value;
                            }
                        }
                    }
                    payload.Add(new LineProtocolPoint(sanitizedName, fields, this.getTags(), maybeDatetime.Value));
                    count++;
                }
                else { ignored++; }
            }

            if (ignored != 0) { Log.Warning($"Ignored {ignored}/{result.Rows} results from table {result.Name}: Could not parse Timestamp"); }
            this.sendPayload(payload, count, $"results ('{result.Name}'{(sanitizedName != result.Name ? $" as '{sanitizedName}'" : "")})");

            OnActivity();
        }

        public override void OnTestPlanRunCompleted(TestPlanRun planRun, Stream logStream)
        {
            string version = PluginManager.GetOpenTapAssembly().SemanticVersion.ToString();
            string hostName = EngineSettings.Current.StationName;

            StreamReader reader = new StreamReader(logStream);
            LineProtocolPayload payload = new LineProtocolPayload();
            int count = 0;

            string line = string.Empty;
            while ((line = reader.ReadLine()) != null)
            {
                LogMessage message = LogMessage.FromLine(line);
                if (message != null && LogLevels.HasFlag(message.Level)) 
                {
                    payload.Add(new LineProtocolPoint(
                        "syslog",
                        new Dictionary<string, object> { // fields
                            { "message", message.Text }
                        },
                        this.getTags("severity", message.SeverityCode),
                        startTime + message.Time)
                    );
                    count++;
                }
            }
            this.sendPayload(payload, count, "log messages");
        }

        private void sendPayload(LineProtocolPayload payload, int count, string kind)
        {
            Log.Info($"Sending {count} {kind} to {Name}");
            try
            {
                LineProtocolWriteResult result = this.client.WriteAsync(payload).GetAwaiter().GetResult();
                if (!result.Success) { throw new Exception(result.ErrorMessage); }
            }
            catch (Exception e)
            {
                Log.Error($"Error while sending payload: {e.Message}{(e.InnerException != null ? $" - {e.InnerException.Message}" : "")}");
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
