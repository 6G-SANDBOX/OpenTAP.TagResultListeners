// Author:      Bruno Garcia Garcia <bgarcia@lcc.uma.es>
// Copyright:   Copyright 2019-2020 Universidad de Málaga (University of Málaga), Spain

using System;
using System.Collections.Generic;
using OpenTap;

using OpenTap.TagResultListeners.Extensions;

namespace OpenTap.TagResultListeners.Steps
{
    [Display("Set Execution Metadata", Group: "Tag Result Listeners",
             Description: "Sets the Execution ID on compatible result listeners Additional\n"+
                          "metadata will be saved on the 'execution_metadata' result.")]
    public class SetExecutionMetadataStep : SetExecutionIdStep
    {
        #region Settings

        [Display("Slice", Order: 2.2, Group: "Metadata")]
        public string Slice { get; set; }

        [Display("Scenario", Order: 2.3, Group: "Metadata" )]
        public string Scenario { get; set; }

        [Display("TestCases", Order: 2.4, Group: "Metadata" )]
        public string TestCases { get; set; }

        [Display("Notes", Order: 2.5, Group: "Metadata" )]
        public string Notes { get; set; }

        #endregion

        private static List<string> columns = new List<string> { "Timestamp", "Date", "Time", "Slice", "Scenario", "TestCases", "Notes" };

        public SetExecutionMetadataStep()
        {
            Slice = Scenario = TestCases = string.Empty;
            Notes = "Test execution";
        }

        public override void Run()
        {
            base.Run();

            DateTime now = DateTime.UtcNow;
            IConvertible[] values = new IConvertible[] {
                now.ToUnixTimestamp(), now.ToShortDateString(), now.ToShortTimeString(), Slice, Scenario, TestCases, Notes
                };

            Results.Publish("execution_metadata", columns, values);

            Log.Info("Experiment metadata: ");
            for (int i = 0; i < columns.Count; i++)
            {
                Log.Info($"  {columns[i]}: {values[i]}");
            }
        }
    }
}
