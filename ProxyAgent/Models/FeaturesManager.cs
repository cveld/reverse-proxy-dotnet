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
        /// <summary>
        /// Defines the key to the default or root feature
        /// </summary>
        public const string DEFAULTFEATURE = "default";
        /// <summary>
        /// Defines the key within a feature to the default url if no app matches
        /// </summary>
        public const string DEFAULTURLKEY = "default";
        /// <summary>
        /// Defines the key within a feature that defines the parent feature
        /// </summary>
        public const string PARENTKEY = "parent";
        /// <summary>
        /// Defines the http header that contains the active feature
        /// </summary>
        public const string HTTPHEADER_FEATURE = "feature";
        /// <summary>
        /// Defines the cookie that contains the active feature
        /// </summary>
        public const string COOKIE_FEATURE = "feature";
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
