// Author:      Bruno Garcia Garcia <bgarcia@lcc.uma.es>
// Copyright:   Copyright 2019-2020 Universidad de Málaga (University of Málaga), Spain

using System;
using OpenTap;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;

namespace Tap.Plugins.UMA.ResultListeners
{
    public class DateTimeOverride {
        [Display( "Result Name", Order: 1 )]
        public string ResultName { get; set; }

        [Display( "Column Name 1", Order: 2 )]
        public string Column1 { get; set; }

        [Display( "DateTime Format 1", Order: 3 )]
        public string Format1 { get; set; }

        [Display( "Column Name 2", Order: 4 )]
        public string Column2 { get; set; }

        [Display( "DateTime Format 2", Order: 5 )]
        public string Format2 { get; set; }

        public DateTimeOverride( ) { }

        public DateTime? Parse( Dictionary<string, IConvertible> row ) {
            if ( !row.Keys.Contains( Column1 ) || ( !string.IsNullOrWhiteSpace( Column2 ) && !row.Keys.Contains( Column2 ) ) ) {
                return null;
            }

            string completeFormat = $"{Format1}||{Format2}";
            string completeValue = $"{row[Column1]}||{( string.IsNullOrWhiteSpace( Column2 ) ? "" : row[Column2] )}";

            if ( DateTime.TryParseExact( completeValue, completeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result ) ) {
                return result.ToUniversalTime();
            } else { return null; }
        }
    }

}
