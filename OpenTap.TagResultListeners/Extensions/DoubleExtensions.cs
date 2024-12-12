// Author:      Bruno Garcia Garcia <bgarcia@lcc.uma.es>
// Copyright:   Copyright 2019-2020 Universidad de Málaga (University of Málaga), Spain

using System;

namespace OpenTap.TagResultListeners.Extensions
{
    public static class DoubleExtensions
    {
        public static DateTime ToDateTime(this double timestamp)
        {
            DateTimeOffset offset = DateTimeOffset.FromUnixTimeMilliseconds((long)(timestamp * 1000));
            return offset.UtcDateTime;
        }
    }
}
