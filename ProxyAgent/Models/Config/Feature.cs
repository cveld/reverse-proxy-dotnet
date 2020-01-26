using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.IoTSolutions.ReverseProxy.Models.Config
{
    public class Feature
    {
        /// <summary>
        /// Name of the feature
        /// </summary>
        public string Name { get; set; }        
        /// <summary>
        /// When a hostname is not matched, the path is matched against the keys of the Urls dictionary
        /// </summary>
        public StringNode Urls { get; set; }
        /// <summary>
        /// Parent gives access to the parent feature configuration
        /// </summary>
        public Feature Parent { get; set; }
    }
}
