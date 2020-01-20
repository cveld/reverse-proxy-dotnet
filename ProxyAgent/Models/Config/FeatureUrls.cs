using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.IoTSolutions.ReverseProxy.Models.Config
{
    public class FeatureUrls
    {
        string DefaultUrl { get; set; }
        Dictionary<string, string> Urls { get; set; }
    }
}
