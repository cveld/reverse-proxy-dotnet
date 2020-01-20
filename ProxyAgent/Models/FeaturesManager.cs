using Microsoft.AspNetCore.Http;
using Microsoft.Azure.IoTSolutions.ReverseProxy.Models.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ReverseProxy.Models
{
    public enum FeatureAvailability
    {
        Unspecified,
        Cookie,
        Header,
        NotPresent
    }

    public class FeaturesManager
    {
        /// <summary>
        /// Defines the key to the default or root feature
        /// </summary>
        public const string DEFAULTFEATURE = "default-feature";
        /// <summary>
        /// Defines the key within a feature to the default url if no app matches
        /// </summary>
        public const string DEFAULTURLKEY = "default-url";
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
        /// <summary>
        /// Prefix for differentiating features from hostnames
        /// </summary>
        public const string HOSTNAME_PREFIX = "hostname:";

        private readonly ILogger<FeaturesManager> logger;
        public Dictionary<string, Dictionary<string, string>> FeaturesOld = new Dictionary<string, Dictionary<string, string>>();
        public FeaturesRoot featuresRoot = new FeaturesRoot
        {
            DefaultHost = new FeaturesConfig
            {
                Features = new Dictionary<string, Feature>()
            },
            Hostnames = new Dictionary<string, FeaturesConfig>()
        };
        public FeaturesManager(ILogger<FeaturesManager> logger)
        {
            this.logger = logger;
        }
        public void ReadConfigOld(string jsonpath)
        {
            var json = File.ReadAllText(jsonpath);
            var jsonobj = JObject.Parse(json);

            foreach (var feature in jsonobj)
            {
                ProcessFeatureOld(feature.Key, feature.Value);
            }
        }

        public void ReadConfig(string jsonpath)
        {
            var json = File.ReadAllText(jsonpath);
            var jsonobj = JObject.Parse(json);
            
            foreach (var rootitem in jsonobj)
            {
                ProcessRootItem(rootitem.Key, rootitem.Value);
            }
        }

        private void ProcessRootItem(string key, JToken value)
        {
            var output = ParseHostname(key);
            if (output.hostnameEnum == HostnameEnum.NoHostname)
            {
                // process a single feature into the list of DefaultHost features
                ProcessFeatureItem(featuresRoot.DefaultHost, key, value);
            }
            else
            {
                var result = featuresRoot.Hostnames.TryGetValue(output.output, out FeaturesConfig features);
                if (!result)
                {
                    features = new FeaturesConfig
                    {
                        Features = new Dictionary<string, Feature>()
                    };
                    featuresRoot.Hostnames.Add(output.output, features);
                }
                ProcessFeatures(features, value);
            }
        }

        /// <summary>
        /// Process a features block (jsonobj) and add discovered features to featuresConfig
        /// </summary>
        /// <param name="featuresConfig"></param>
        /// <param name="output"></param>
        /// <param name="jsonobj"></param>
        private void ProcessFeatures(FeaturesConfig featuresConfig, JToken jsonobj)
        {            
            foreach (var featureitem in (JObject)jsonobj)
            {                
                ProcessFeatureItem(featuresConfig, featureitem.Key, featureitem.Value);
            }
        }

        /// <summary>
        /// Process a feature block (jsonobj) and add discovered feature configuration to an existing or new feature object
        /// </summary>
        /// <param name="featuresConfig"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        private void ProcessFeatureItem(FeaturesConfig featuresConfig, string key, JToken jsonobj)
        {
            var result = featuresConfig.Features.TryGetValue(key, out Feature feature);
            if (!result)
            {
                feature = new Feature
                {
                    Name = key,
                    Urls = new StringNode
                    {
                        Children = new Dictionary<string, StringNode>()
                    }
                };
                featuresConfig.Features.Add(key, feature);
            }
            foreach (var url in (JObject)jsonobj)
            {
                if (url.Key == PARENTKEY)
                {
                    var parentkey = url.Value.ToString();
                    bool parentfound = featuresConfig.Features.TryGetValue(parentkey, out Feature parent);
                    if (!parentfound)
                    {
                        logger.LogWarning("Parent feature must be specified above child feature {parent} {child}", parentkey, key);
                    }
                    else
                    {
                        feature.Parent = parent;
                    }

                    continue;
                }

                var output = ParseHostname(url.Key);
                if (output.hostnameEnum == HostnameEnum.NoHostname)
                {
                    // it is a url
                    // feature.Urls.Add(output.output, url.Value.ToString());
                    var value = url.Value.ToString();
                    AddPathToUrls(feature, output.output, value);
                }
                else
                {
                    // it is a hostname
                    logger.LogWarning("Hostnames within feature configuration is not allowed {feature} {hostname}", key, output.output);
                }
            }
        }

        /// <summary>
        /// Add a path to a tree of url segments within a feature object
        /// </summary>
        /// <param name="path"></param>
        private void AddPathToUrls(Feature feature, string path, string value)
        {
            var split = path.Split("/");
            var currentNode = feature.Urls;
            foreach (var label in split)
            {
                var result = currentNode.Children.TryGetValue(label, out StringNode child);
                if (result)
                {
                    currentNode = child;
                    continue;
                }
                var newChild = new StringNode
                {
                    Label = label,
                    Children = new Dictionary<string, StringNode>()
                };
                currentNode.Children.Add(label, newChild);
                newChild.Parent = currentNode;
                currentNode = newChild;
            }
            currentNode.Value = value;
        }

        enum HostnameEnum
        {
            NotSpecified,
            Hostname,
            NoHostname
        }

        private (string output, HostnameEnum hostnameEnum) ParseHostname(string input)
        {
            if (input.StartsWith(HOSTNAME_PREFIX))
            {
                var outputhostname = input.Substring(HOSTNAME_PREFIX.Length);
                return (outputhostname, HostnameEnum.Hostname);
            }

            return (input, HostnameEnum.NoHostname);
        }


        private void ProcessFeatureOld(string name, JToken token)
        {
            var dict = new Dictionary<string, string>();
            FeaturesOld[name] = dict;

            // get parent feature key
            var parent = token[PARENTKEY];

            if (parent != null) {
                // copy all application settings into child
                foreach (var item in FeaturesOld[parent.ToString()])
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

       
        /// <summary>
        /// Gets the current feature from the cookie of header of the given HttpRequest
        /// </summary>
        /// <param name="requestIn"></param>
        /// <returns></returns>
        public (string feature, FeatureAvailability featureAvailability) GetFeatureFromCookieOrHeader(HttpRequest requestIn)
        {
            string feature;
            FeatureAvailability featureAvailability;
            // Check http header for feature configuration:
            var result = requestIn.Headers.TryGetValue(FeaturesManager.HTTPHEADER_FEATURE, out StringValues stringValues);

            if (result)
            {
                feature = stringValues[0];
                featureAvailability = FeatureAvailability.Header;
            }
            else
            {
                // if http header not present, check cookie:
                if (requestIn.Cookies.TryGetValue(FeaturesManager.COOKIE_FEATURE, out feature))
                {
                    // feature configuration not yet available in http header. Add it from cookie:
                    featureAvailability = FeatureAvailability.Cookie;
                }
                else
                {
                    featureAvailability = FeatureAvailability.NotPresent;
                }
            }
            return (feature, featureAvailability);
        }

        /// <summary>
        /// Derives the target url from feature configuration and given HttpRequest
        /// </summary>
        /// <param name="feature"></param>
        /// <param name="requestIn"></param>
        /// <returns></returns>
        public (string fromSchemeHostname, string fromUrl, string toFeatureUrl, string toUrl)? GetUrlFromFeatureConfiguration(string feature, HttpRequest requestIn)
        {
            string fromSchemeHostname;
            string fromUrl;
            string toFeatureUrl;
            string toUrl;

            if (FeaturesOld == null)
            {
                return null;
            }
            var success = FeaturesOld.TryGetValue(feature ?? FeaturesManager.DEFAULTFEATURE, out var activefeature);
            if (!success)
            {
                logger.LogError($"Feature not present: {feature ?? FeaturesManager.DEFAULTFEATURE}", feature);
                return null;
            }

            var segments = requestIn.Path.Value.Split('/');
            
            if (segments.Length <= 1 || !activefeature.ContainsKey(segments[1]))
            {
                // requested app not found in feature configuration, forward request to the default:
                toFeatureUrl = activefeature[FeaturesManager.DEFAULTURLKEY];
                toUrl = toFeatureUrl + (!requestIn.Path.HasValue || string.IsNullOrEmpty(requestIn.Path.Value) ? String.Empty : ("/" + requestIn.Path.Value)) + requestIn.QueryString;
                fromUrl = requestIn.Scheme + "://" + requestIn.Host;
                fromSchemeHostname = requestIn.Scheme + "://" + requestIn.Host;
            }
            else
            {
                toFeatureUrl = activefeature[segments[1]];
                var path = string.Join("/", segments.Skip(2));
                toUrl = toFeatureUrl + "/" + path + requestIn.QueryString;
                fromUrl = requestIn.Scheme + "://" + requestIn.Host + "/" + segments[1];
                fromSchemeHostname = requestIn.Scheme + "://" + requestIn.Host;
            }

            logger.LogDebug("URL {toUrl}", toUrl);
            return (fromSchemeHostname, fromUrl, toFeatureUrl, toUrl);
        }
    }
}
