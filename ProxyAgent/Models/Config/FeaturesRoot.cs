using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.IoTSolutions.ReverseProxy.Models.Config
{
    public class FeaturesRoot
    {
        /// <summary>
        /// When the incoming hostname is not matched against the list of dictionary keys, the feature will be looked up in DefaultHostname features list
        /// </summary>
        public FeaturesConfig DefaultHost { get; set; }
        public Dictionary<string, FeaturesConfig> Hostnames { get; set; }
    }
}
