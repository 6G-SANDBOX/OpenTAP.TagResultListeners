// Author:      Bruno Garcia Garcia <bgarcia@lcc.uma.es>
// Copyright:   Copyright 2019-2020 Universidad de Málaga (University of Málaga), Spain

namespace OpenTap.TagResultListeners.Enums
{
    public static class CsvSeparatorExtensions
    {
        public static string AsString(this CsvSeparator separator)
        {
            switch (separator)
            {
                case CsvSeparator.Comma: return ",";
                case CsvSeparator.SemiColon: return ";";
                default: return "\t";
            }
        }

        public static string DefaultReplacement(this CsvSeparator separator)
        {
            return separator == CsvSeparator.SemiColon ? "," : ";";
        }
    }
}