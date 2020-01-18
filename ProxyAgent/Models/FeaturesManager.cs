using Microsoft.AspNetCore.Http;
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
        private readonly ILogger<FeaturesManager> logger;
        public Dictionary<string, Dictionary<string, string>> Features = new Dictionary<string, Dictionary<string, string>>();
        public FeaturesManager(ILogger<FeaturesManager> logger)
        {
            this.logger = logger;
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

            if (Features == null)
            {
                return null;
            }
            var success = Features.TryGetValue(feature ?? FeaturesManager.DEFAULTFEATURE, out var activefeature);
            if (!success)
            {
                logger.LogError($"Feature not present: {feature ?? FeaturesManager.DEFAULTFEATURE}", feature);
                return null;
            }

            var segments = requestIn.Path.Value.Split('/');
            
            if (!activefeature.ContainsKey(segments[1]))
            {
                // requested app not found in feature configuration, forward request to the default:
                toFeatureUrl = activefeature[FeaturesManager.DEFAULTURLKEY];
                toUrl = toFeatureUrl + "/" + requestIn.Path.Value + requestIn.QueryString;
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
