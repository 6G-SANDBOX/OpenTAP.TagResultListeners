// Author:      Bruno Garcia Garcia <bgarcia@lcc.uma.es>
// Copyright:   Universidad de Málaga (University of Málaga), Spain

namespace OpenTap.Plugins.UMA.ResultListeners
{
    public class MqttPublisherOverride {

        [Display( "Result Name", Order: 1 )]
        public string ResultName { get; set; }

        [Display( "Column Name", Order: 2 )]
        public string Column { get; set; }

        [Display( "Type (name override)", Order: 3 )]
        public string Type { get; set; }

        [Display( "Unit", Order: 4 )]
        public string Unit { get; set; }

        [Display( "Origin", Order: 5 )]
        public string Origin { get; set; }

        public MqttPublisherOverride( ) { }
    }
}
