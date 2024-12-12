// Author:      Bruno Garcia Garcia <bgarcia@lcc.uma.es>
// Copyright:   Copyright 2019-2020 Universidad de Málaga (University of Málaga), Spain

using System;

namespace OpenTap.InfluxDb.ResultListeners
{
    [Flags]
    public enum LogLevel
    {
        Debug = 1,
        Info = 2,
        Warning = 4,
        Error = 8 
    }
}
