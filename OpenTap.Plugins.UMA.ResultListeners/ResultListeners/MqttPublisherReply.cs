// Author:      Bruno Garcia Garcia <bgarcia@lcc.uma.es>
// Copyright:   Universidad de Málaga (University of Málaga), Spain

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTap.Plugins.UMA.ResultListeners {
    class MqttPublisherReply {
        public string Status { get; set; }

        public string Message { get; set; }
    }
}
