using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.IoTSolutions.ReverseProxy.Models.Config
{
    public class FeaturesConfig
    {
        /// <summary>
        /// Dictionary with mapping from feature name to feature object
        /// </summary>
        public Dictionary<string, Feature> Features;
    }
}
