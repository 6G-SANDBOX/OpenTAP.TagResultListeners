// Author:      Bruno Garcia Garcia <bgarcia@lcc.uma.es>
// Copyright:   Copyright 2019-2020 Universidad de Málaga (University of Málaga), Spain


using System.Collections.Generic;
using OpenTap;

using Tap.Plugins.UMA.ResultListeners;

namespace Tap.Plugins.UMA.Steps
{
    [Display("Set Execution ID", Groups: new string[] { "UMA", "Misc" }, 
             Description: "Sets the Execution ID on compatible result listeners. For setting\n"+
                          "additional metadata use the 'Set Experiment Metadata' step.")]
    public class SetExecutionIdStep : TestStep
    {
        #region Settings
        
        [Display("ResultListeners", Order: 1.0)]
        public List<ConfigurableResultListenerBase> ResultListeners { get; set; }

        [Display("Execution ID", Order: 1.1)]
        public string ExecutionId { get; set; }

        #endregion
        public SetExecutionIdStep() { }

        public override void Run()
        {
            if (string.IsNullOrWhiteSpace(ExecutionId))
            {
                Log.Error("Cannot set ExecutionId to an empty string.");
            }
            else
            {
                foreach (ConfigurableResultListenerBase resultListener in ResultListeners)
                {
                    Log.Info($"Setting ExecutiontId to {ExecutionId} ({resultListener.Name})");
                    resultListener.ExecutionId = this.ExecutionId;
                }
            }
        }
    }
}
