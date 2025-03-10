﻿// Author:      Bruno Garcia Garcia <bgarcia@lcc.uma.es>
// Copyright:   Copyright 2019-2020 Universidad de Málaga (University of Málaga), Spain

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using OpenTap;
using System.Xml.Serialization;
using System.Text.RegularExpressions;

using OpenTap.TagResultListeners.Enums;
using OpenTap.TagResultListeners.ResultListeners;

namespace OpenTap.TagResultListeners.ResultListeners.MultiCsv
{
    [Display("Multi CSV", Group: "Tag Result Listeners",
        Description: "Logs results to multiple CSV files, one for each kind of generated results.")]

    public class MultipleCsvResultListener : ConfigurableResultListenerBase
    {
        private const string RESULT_MACRO = "{ResultType}";
        private const string RESULTS_ID_MACRO = "{Identifier}";
        private const string VERDICT_MACRO = "{Verdict}";
        private const string DATE_MACRO = "{Date}";

        private const string DEFAULT_FILE_PATH = @"Results\" + DATE_MACRO + "-" + RESULT_MACRO + "-" + VERDICT_MACRO + ".csv";
        private const string UNDEFINED = "[UNDEFINED_ID]";

        #region Settings

        [Display("CSV separator",
            Group: "CSV",
            Description: "Select which separator to use in the CSV file.",
            Order: 1.0)]
        public CsvSeparator Separator
        {
            get { return _separator; }
            set
            {
                if (value != _separator)
                {
                    _separator = value;
                    if (SeparatorReplacement.IsEnabled && SeparatorReplacement.Value.Equals(separator))
                    {
                        SeparatorReplacement.Value = _separator.DefaultReplacement();
                    }
                }
            }
        }
        private CsvSeparator _separator;

        [Display("Replace separator with", Group: "CSV", Order: 1.1,
            Description: "Replace the separator with this character automatically when found within values")]
        public Enabled<string> SeparatorReplacement { get; set; }

        [Display("File Path", Group: "CSV", Order: 1.2,
            Description: "CSV output path. Available macros are:\n" +
                " - Result type: " + RESULT_MACRO + " (Mandatory)\n" +
                " - Run Identifier: " + RESULTS_ID_MACRO + " (Mandatory if 'Set Execution ID' is enabled)\n" +
                " - Run Verdict: " + VERDICT_MACRO + "\n" +
                " - Run Start Time: " + DATE_MACRO
            )]
        [FilePath(behavior: FilePathAttribute.BehaviorChoice.Save, fileExtension: "csv")]
        public string FilePath { get; set; }

        [XmlIgnore]
        public string Identifier { get; set; }

        #endregion

        private Dictionary<string, PublishedResults> results;
        private List<TestStepRun> stepRuns;
        private TestPlanRun planRun;

        private string separator { get { return Separator.AsString(); } }

        public MultipleCsvResultListener()
        {
            Name = "MultiCSV";

            Separator = CsvSeparator.Comma;
            SeparatorReplacement = new Enabled<string> { IsEnabled = true, Value = ";" };
            FilePath = DEFAULT_FILE_PATH;
            ExecutionId = UNDEFINED;

            Rules.Add(() => FilePath.Contains(RESULT_MACRO), RESULT_MACRO + " must be included on the file path", "FilePath");
            Rules.Add(() => !SetExecutionId || FilePath.Contains(RESULTS_ID_MACRO),
                RESULTS_ID_MACRO + " must be included on the file path", "FilePath");
        }

        #region ResultListener methods

        public override void OnTestPlanRunStart(TestPlanRun planRun)
        {
            results = new Dictionary<string, PublishedResults>();
            stepRuns = new List<TestStepRun>();
            this.planRun = planRun;
            ExecutionId = UNDEFINED;

            base.OnTestPlanRunStart(planRun);
        }

        public override void OnTestStepRunStart(TestStepRun stepRun)
        {
            stepRuns.Add(stepRun);

            base.OnTestStepRunStart(stepRun);
        }

        public override void OnResultPublished(Guid stepRunId, ResultTable result)
        {
            result = ProcessResult(result);
            if (result == null) { return; }

            if (SetExecutionId) { result = InjectColumn(result, "ExecutionId", ExecutionId); }

            string name = result.Name;
            TestStepRun stepRun = getTestStepRun(stepRunId);

            if (!results.ContainsKey(name))
            {
                results[name] = new PublishedResults(result, planRun, stepRun);
            }
            else
            {
                results[name].AddResults(result, stepRun);
            }

            base.OnResultPublished(stepRunId, result);
        }

        public override void OnTestPlanRunCompleted(TestPlanRun planRun, Stream logStream)
        {
            foreach (PublishedResults result in results.Values)
            {
                string output = getResultPath(result.Name, planRun);
                Log.Info("Saving '{0}' results to file '{1}'", result.Name, output);

                string folder = Path.GetDirectoryName(output);
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                    Log.Info("Created folder: {0}", folder);
                }

                using (StreamWriter writer = new StreamWriter(output))
                {
                    // Write the CSV header
                    writer.WriteLine(string.Join(separator, result.Header));

                    // Write the rows of results
                    foreach (List<string> values in result.RowValues)
                    {
                        if (SeparatorReplacement.IsEnabled)
                        {
                            for (int i = 0; i < values.Count; i++) { values[i] = values[i].Replace(separator, SeparatorReplacement.Value); }
                        }

                        writer.WriteLine(string.Join(separator, values));
                    }
                }
            }
            Log.Info("All results saved.");

            this.planRun = null;
            stepRuns = null;
            results = null;

            base.OnTestPlanRunCompleted(planRun, logStream);
        }

        #endregion

        private string getResultPath(string name, TestPlanRun planRun)
        {
            string path = FilePath;

            path = path.Replace(RESULT_MACRO, Sanitize(name, "_"));
            path = path.Replace(VERDICT_MACRO, planRun.Verdict.ToString());
            path = path.Replace(DATE_MACRO, planRun.StartTime.ToString("yyyy-MM-dd HH-mm-ss"));

            if (SetExecutionId)
            {
                if (string.IsNullOrWhiteSpace(ExecutionId))
                {
                    Log.Warning("Results identifier not set, will be " + UNDEFINED);
                    ExecutionId = UNDEFINED;
                }

                string safeIdentifier = Sanitize(ExecutionId, "_");
                Log.Info("Marking results with identifier: " + safeIdentifier);
                path = path.Replace(RESULTS_ID_MACRO, safeIdentifier);
            }

            Log.Debug($"MultiCSV: Path for result '{name}': '{path}'");
            return path;
        }

        private TestStepRun getTestStepRun(Guid stepRunId)
        {
            IEnumerable<TestStepRun> stepRun = stepRuns.Where((s) => stepRunId == s.Id);
            if (stepRun.Count() == 0) { throw new Exception("Unable to find requested StepRunId"); }

            return stepRun.ElementAt(0);
        }
    }
}
