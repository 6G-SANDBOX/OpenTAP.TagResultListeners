// Author:      Bruno Garcia Garcia <bgarcia@lcc.uma.es>
// Copyright:   Copyright 2016-2017 Universidad de Málaga (University of Málaga), Spain

using System.Collections.Generic;

using OpenTap;

namespace OpenTap.TagResultListeners.ResultListeners.MultiCsv
{
    internal class RowData
    {
        public TestStepRun StepRun { get; private set; }
        private Dictionary<string, string> values;

        public RowData(TestStepRun stepRun)
        {
            values = new Dictionary<string, string>();
            StepRun = stepRun;
        }

        public string this[string name]
        {
            get { return values.ContainsKey(name) ? values[name] : string.Empty; }
            set { values[name] = value; }
        }
    }
}