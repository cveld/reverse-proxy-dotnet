// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.IoTSolutions.ReverseProxy;
using Microsoft.Azure.IoTSolutions.ReverseProxy.Diagnostics;
using Microsoft.Azure.IoTSolutions.ReverseProxy.Exceptions;
using Microsoft.Azure.IoTSolutions.ReverseProxy.HttpClient;
using Microsoft.Azure.IoTSolutions.ReverseProxy.Models;
using Microsoft.Azure.IoTSolutions.ReverseProxy.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using ReverseProxy.HttpClient;
using HttpRequest = Microsoft.AspNetCore.Http.HttpRequest;
using HttpResponse = Microsoft.AspNetCore.Http.HttpResponse;
using Microsoft.AspNetCore.Http.Extensions;
using ReverseProxy.Models;

namespace ReverseProxy
{
    public interface IProxy
    {
        Task<ProxyStatus> PingAsync();

        Task ProcessAsync(            
            HttpRequest requestIn,
            HttpResponse responseOut);
    }


    // HttpRequest scoped
    public class Proxy : IProxy
    {
        private const string LOCATION_HEADER = "Location";
        private const string HSTS_HEADER = "Strict-Transport-Security";

        // Headers not forwarded to the remote endpoint
        private static readonly HashSet<string> ExcludedRequestHeaders =
            new HashSet<string>
            {
                "connection",
                "content-length",
                "keep-alive",
                "host",
                "upgrade",
                "upgrade-insecure-requests"
            };

        // Headers returned by the remote endpoint
        // which are not returned to the client
        private static readonly HashSet<string> ExcludedResponseHeaders =
            new HashSet<string>
            {
                "connection",
                "server",
                "transfer-encoding",
                "upgrade",
                "x-powered-by",
                HSTS_HEADER.ToLowerInvariant()
            };
        private readonly FeaturesManager featuresManager;
        private readonly IHttpClient client;
        private readonly IConfig config;
        private readonly ILogger<Proxy> log;

        private string fromSchemeHostname;
        private string fromUrl;
        private string toSchemeHostname;
        private string toUrl;
        private string feature;

        public Proxy(
            FeaturesManager featuresManager,
            IHttpClient httpclient,
            IConfig config,
            ILogger<Proxy> log)
        {
            this.featuresManager = featuresManager;
            this.client = httpclient;
            this.config = config;
            this.log = log;
        }

        public async Task<ProxyStatus> PingAsync()
        {
            var request = new HttpClient.HttpRequest();
            //request.SetUriFromString(this.config.Endpoint);
            request.Options.EnsureSuccess = false;
            request.Options.Timeout = 5000;

            // The HTTP client uses cert. pinning, allowing self-signed certs
            request.Options.AllowInsecureSslServer = true;

            var response = await this.client.GetAsync(request);

            var body = response.Content == null
                ? string.Empty
                : Encoding.UTF8.GetString(response.Content);
            var content = body.Length > 120
                ? body.Substring(0, 120) + "..."
                : body;

            return new ProxyStatus
            {
                StatusCode = (int) response.StatusCode,
                Message = content
            };
        }

        public async Task ProcessAsync(            
            HttpRequest requestIn,
            HttpResponse responseOut)
        {
            IHttpRequest request;

            try
            {
                this.RedirectToHttpsIfNeeded(requestIn);
                request = this.BuildRequest(requestIn);
                if (request == null)
                {
                    // requested app cannot be found in configuration, return 404:
                    responseOut.StatusCode = 404;
                    return;
                }
            }
            catch (RequestPayloadTooLargeException)
            {
                responseOut.StatusCode = (int) HttpStatusCode.RequestEntityTooLarge;
                ApplicationRequestRouting.DisableInstanceAffinity(responseOut);
                return;
            }
            catch (RedirectException e)
            {
                responseOut.StatusCode = (int) e.StatusCode;
                responseOut.Headers[LOCATION_HEADER] = e.Location;
                ApplicationRequestRouting.DisableInstanceAffinity(responseOut);
                return;
            }

            IHttpResponse response;
            var method = requestIn.Method.ToUpperInvariant();
            this.log.LogDebug("Request method", method);
            switch (method)
            {
                case "GET":
                    response = await this.client.GetAsync(request);
                    break;
                case "DELETE":
                    response = await this.client.DeleteAsync(request);
                    break;
                case "OPTIONS":
                    response = await this.client.OptionsAsync(request);
                    break;
                case "HEAD":
                    response = await this.client.HeadAsync(request);
                    break;
                case "POST":
                    response = await this.client.PostAsync(request);
                    break;
                case "PUT":
                    response = await this.client.PutAsync(request);
                    break;
                case "PATCH":
                    response = await this.client.PatchAsync(request);
                    break;
                default:
                    // Note: this could flood the logs due to spiders...
                    this.log.LogInformation("Request method not supported", new { method });
                    responseOut.StatusCode = (int) HttpStatusCode.NotImplemented;
                    ApplicationRequestRouting.DisableInstanceAffinity(responseOut);
                    return;
            }

            await this.BuildResponseAsync(response, responseOut, request, requestIn);
        }

        private void RedirectToHttpsIfNeeded(HttpRequest requestIn)
        {
            if (requestIn.IsHttps || !this.config.RedirectHttpToHttps) return;

            var location = "https://" + requestIn.Host + requestIn.Path.Value + requestIn.QueryString;
            throw new RedirectException(HttpStatusCode.Moved, location);
        }

        // Prepare the request to send to the remote endpoint
        private IHttpRequest BuildRequest(HttpRequest requestIn)
        {
            var requestOut = new HttpClient.HttpRequest();

            // Forward HTTP request headers
            foreach (var header in requestIn.Headers)
            {
                if (ExcludedRequestHeaders.Contains(header.Key.ToLowerInvariant()))
                {
                    this.log.LogDebug("Ignoring request header", new { header.Key, header.Value });
                    continue;
                }

                this.log.LogDebug("Adding request header", new { header.Key, header.Value });
                foreach (var value in header.Value)
                {
                    requestOut.AddHeader(header.Key, value);
                }
            }

            FeatureAvailability featureAvailability;
            (feature, featureAvailability) = featuresManager.GetFeatureFromCookieOrHeader(requestIn);
            if (featureAvailability == FeatureAvailability.Cookie)
            {
                // feature available in cookie, but not as http header. Add to outgoing http headers:
                requestOut.AddHeader(FeaturesManager.HTTPHEADER_FEATURE, feature);
            }
            var urls = featuresManager.GetUrlFromFeatureConfiguration(feature, requestIn);
            fromSchemeHostname = urls?.fromSchemeHostname;
            fromUrl = urls?.fromUrl;
            requestOut.SetUriFromString(urls?.toUrl ?? requestIn.GetEncodedUrl());

            // Forward request payload
            var method = requestIn.Method.ToUpperInvariant();
            if (Microsoft.Azure.IoTSolutions.ReverseProxy.HttpClient.HttpClient.MethodsWithPayload.Contains(method))
            {
                requestOut.SetContent(this.GetRequestPayload(requestIn), requestIn.ContentType);
            }

            // Allow error codes without throwing an exception
            requestOut.Options.EnsureSuccess = false;

            // The HTTP client uses cert. pinning, allowing self-signed certs
            requestOut.Options.AllowInsecureSslServer = true;

            return requestOut;
        }

        

        

        private async Task BuildResponseAsync(IHttpResponse response, HttpResponse responseOut, IHttpRequest request, HttpRequest requestIn)
        {
            // Forward the HTTP status code
            this.log.LogDebug("Status code", new { response.StatusCode });
            responseOut.StatusCode = (int) response.StatusCode;

            // The Headers property can be null in case of errors
            if (response.Headers != null)
            {
                // Forward the HTTP headers
                foreach (var header in response.Headers)
                {
                    if (ExcludedResponseHeaders.Contains(header.Key.ToLowerInvariant()))
                    {
                        this.log.LogDebug("Ignoring response header", new { header.Key, header.Value });
                        continue;
                    }                    

                    this.log.LogDebug("Adding response header", new { header.Key, header.Value });
                    foreach (var incomingvalue in header.Value)
                    {
                        var value = incomingvalue;
                        if (header.Key == "Location")
                        {
                            // rewrite redirect url
                            var fromSchemeHostname = requestIn.Scheme + "://" + requestIn.Host;
                            var toSchemeHostname = request.Uri.Scheme + "://" + request.Uri.Host;
                            value = value.Replace(toSchemeHostname, fromSchemeHostname);
                        }

                        if (!responseOut.Headers.ContainsKey(header.Key))
                        {
                            responseOut.Headers[header.Key] = value;
                        }
                        else
                        {
                            // `.Append()` doesn't work on responseOut.Headers, this is
                            // a workaround to support multiple instances of the same header
                            var headers = responseOut.Headers[header.Key].ToList();
                            headers.Add(value);
                            responseOut.Headers[header.Key] = new StringValues(headers.ToArray());
                        }
                    }
                }
            }

            // HSTS support
            // See: https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Strict-Transport-Security
            // Note: The Strict-Transport-Security header is ignored by the browser when your
            // site is accessed using HTTP; this is because an attacker may intercept HTTP
            // connections and inject the header or remove it.
            if (requestIn.IsHttps && this.config.StrictTransportSecurityEnabled)
            {
                responseOut.Headers[HSTS_HEADER] = "max-age=" + this.config.StrictTransportSecurityPeriod;
            }

            // Last header before writing to the socket
            ApplicationRequestRouting.DisableInstanceAffinity(responseOut);

            // Some status codes like 204 and 304 can't have a body
            if (response.CanHaveBody && response.Content.Length > 0)
            {
                var rewritepayload = RewritePayload(requestIn, request, response);
                responseOut.Headers["Content-Length"] = rewritepayload.Length.ToString();
                await responseOut.Body.WriteAsync(rewritepayload, 0, rewritepayload.Length);
            }
        }

        byte[] RewritePayload(HttpRequest requestIn, IHttpRequest request, IHttpResponse response)
        {
            var content = response.Content;
            var contenttypekv = response.Headers?.Where(h => h.Key.ToLower() == "content-type").FirstOrDefault();
            var contenttype = contenttypekv?.Value.FirstOrDefault();
            if (contenttype?.Contains(";") == true)
            {
                contenttype = contenttype.Substring(0, contenttype.IndexOf(";"));
            }
            contenttype = contenttype?.ToLower();

            if (contenttype == "text/html" || contenttype == "text/javascript" || contenttype == "application/json")            
            {
                var stringContent = Encoding.UTF8.GetString(content);
                var fromSchemeHostname = requestIn.Scheme + "://" + requestIn.Host;
                var toSchemeHostname = request.Uri.Scheme + "://" + request.Uri.Host;
                var newContent = stringContent.Replace(toSchemeHostname, fromSchemeHostname);
                return Encoding.UTF8.GetBytes(newContent);
            }
            else
            {
                return content;
            }
        }

        private byte[] GetRequestPayload(HttpRequest request)
        {
            using (var memstream = new MemoryStream())
            {
                request.Body.CopyTo(memstream);
                return memstream.ToArray();
            }
        }
    }
}
