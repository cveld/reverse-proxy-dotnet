using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.IoTSolutions.ReverseProxy.Models
{
    public class FeaturesManager
    {
        const string DEFAULTFEATURE = "default";
        const string PARENTKEY = "parent";
        public Dictionary<string, Dictionary<string, string>> Features = new Dictionary<string, Dictionary<string, string>>();
        public FeaturesManager()
        {

        }
        public void ReadConfig(string jsonpath)
        {
            var json = File.ReadAllText(jsonpath);
            var jsonobj = JObject.Parse(json);

            foreach (var feature in jsonobj)
            {
                ProcessFeature(feature.Key, feature.Value);
            }

        }

        private void ProcessFeature(string name, JToken token)
        {
            var dict = new Dictionary<string, string>();
            Features[name] = dict;

            // get parent feature key
            var parent = token[PARENTKEY];

            if (parent != null) {
                // copy all application settings into child
                foreach (var item in Features[parent.ToString()])
                {
                    dict.Add(item.Key, item.Value);
                }
            }

            foreach (var item in token)
            {
                var prop = item as JProperty;
                if (prop.Name != PARENTKEY)
                {
                    if (dict.ContainsKey(prop.Name))
                    {
                        dict[prop.Name] = prop.Value.ToString();
                    }
                    else
                    {
                        dict.Add(prop.Name, prop.Value.ToString());
                    }
                }
            }
        }
    }
}
