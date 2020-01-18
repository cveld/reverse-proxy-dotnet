// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.IoTSolutions.ReverseProxy.Diagnostics;
using Microsoft.Azure.IoTSolutions.ReverseProxy.Runtime;
using Microsoft.Extensions.Logging;
using ReverseProxy.HttpClient;

namespace Microsoft.Azure.IoTSolutions.ReverseProxy.HttpClient
{
    public interface IHttpClient
    {
        Task<IHttpResponse> GetAsync(IHttpRequest request);

        Task<IHttpResponse> PostAsync(IHttpRequest request);

        Task<IHttpResponse> PutAsync(IHttpRequest request);

        Task<IHttpResponse> PatchAsync(IHttpRequest request);

        Task<IHttpResponse> DeleteAsync(IHttpRequest request);

        Task<IHttpResponse> HeadAsync(IHttpRequest request);

        Task<IHttpResponse> OptionsAsync(IHttpRequest request);
    }

    public class HttpClient : IHttpClient
    {
        private readonly ILogger<HttpClient> log;
        private readonly IConfig config;
        private const string CONTENT_TYPE_HEADER = "Content-Type";

        public static HashSet<string> MethodsWithPayload => new HashSet<string> { "POST", "PUT", "PATCH" };

        public HttpClient(ILogger<HttpClient> logger, IConfig config)
        {
            this.log = logger;
            this.config = config;
        }

        public async Task<IHttpResponse> GetAsync(IHttpRequest request)
        {
            var result = await this.SendAsync(request, HttpMethod.Get);
            return result;
        }

        public async Task<IHttpResponse> PostAsync(IHttpRequest request)
        {
            return await this.SendAsync(request, HttpMethod.Post);
        }

        public async Task<IHttpResponse> PutAsync(IHttpRequest request)
        {
            return await this.SendAsync(request, HttpMethod.Put);
        }

        public async Task<IHttpResponse> PatchAsync(IHttpRequest request)
        {
            return await this.SendAsync(request, new HttpMethod("PATCH"));
        }

        public async Task<IHttpResponse> DeleteAsync(IHttpRequest request)
        {
            return await this.SendAsync(request, HttpMethod.Delete);
        }

        public async Task<IHttpResponse> HeadAsync(IHttpRequest request)
        {
            return await this.SendAsync(request, HttpMethod.Head);
        }

        public async Task<IHttpResponse> OptionsAsync(IHttpRequest request)
        {
            return await this.SendAsync(request, HttpMethod.Options);
        }

        static System.Net.Http.HttpClient httpClient;
        static SocketsHttpHandler socketsHttpHandler;

        static HttpClient()
        {
            socketsHttpHandler = new SocketsHttpHandler();
            socketsHttpHandler.AllowAutoRedirect = false;
            httpClient = new System.Net.Http.HttpClient(socketsHttpHandler);
        }

        private async Task<IHttpResponse> SendAsync(IHttpRequest request, HttpMethod httpMethod)
        {
            // var clientHandler = new HttpClientHandler();
            var client = httpClient;
            
            var httpRequest = new HttpRequestMessage
            {
                Method = httpMethod,
                
                RequestUri = request.Uri
            };

            // Keep track of headers added on the content to avoid duplications
            // which could lead to invalid headers like
            // -> "Content-Type: application/json, application/json"
            var headersOnContentObject = new HashSet<string>();

            this.SetServerSslSecurity(request, socketsHttpHandler);            

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(request.Options.Timeout));            
            
            // Note: SetContent must be called before SetHeaders to prioritize the
            // Content Type value set in the content.
            // TODO: ensure that's what happens with a unit test
            this.SetContent(request, httpMethod, httpRequest, ref headersOnContentObject);
            this.SetHeaders(request, httpRequest, ref headersOnContentObject);

            this.log.LogDebug("Sending request", new { httpMethod, request.Uri, request.Options });

            try
            {
                using (var response = await client.SendAsync(httpRequest, cts.Token))
                {
                    if (request.Options.EnsureSuccess) response.EnsureSuccessStatusCode();

                    var headers = new HttpHeaders();
                    foreach (var header in response.Headers)
                    {
                        headers.Add(header.Key, header.Value);
                    }

                    foreach (var header in response.Content.Headers)
                    {
                        headers.Add(header.Key, header.Value);
                    }                    

                    return new HttpResponse
                    {
                        StatusCode = response.StatusCode,
                        Headers = headers,                        
                        Content = await response.Content.ReadAsByteArrayAsync()
                    };
                }
            }
            catch (HttpRequestException e)
            {
                var errorMessage = e.Message;
                if (e.InnerException != null)
                {
                    errorMessage += " - " + e.InnerException.Message;
                }

                this.log.LogError("Request failed", new { errorMessage, e });

                return new HttpResponse
                {
                    StatusCode = HttpStatusCode.BadGateway,
                    Content = Encoding.UTF8.GetBytes(errorMessage)
                };
            }
            catch (PlatformNotSupportedException e)
            {
                // For instance, on some OSes, .NET Core doesn't yet
                // support ServerCertificateCustomValidationCallback

                this.log.LogError("Sorry, your system does not support the requested feature.",
                    new { e });

                return new HttpResponse
                {
                    StatusCode = 0,
                    Content = Encoding.UTF8.GetBytes(e.Message)
                };
            }
            catch (TaskCanceledException e)
            {
                this.log.LogError("Request failed",
                    new
                    {
                        Message = e.Message + " The request timed out, the endpoint might be unreachable.",
                        e
                    });

                return new HttpResponse
                {
                    StatusCode = 0,
                    Content = Encoding.UTF8.GetBytes(e.Message + " The endpoint might be unreachable.")
                };
            }
            catch (Exception e)
            {
                this.log.LogError("Request failed", new { e.Message, e });

                return new HttpResponse
                {
                    StatusCode = 0,
                    Content = Encoding.UTF8.GetBytes(e.Message)
                };
            }
            
        }

        private void SetContent(
            IHttpRequest request,
            HttpMethod httpMethod,
            HttpRequestMessage httpRequest,
            ref HashSet<string> headersOnContentObject)
        {
            if (!MethodsWithPayload.Contains(httpMethod.Method.ToUpperInvariant())) return;

            httpRequest.Content = request.Content;
            if (request.ContentType != null && request.Content != null)
            {
                headersOnContentObject.Add(CONTENT_TYPE_HEADER.ToLowerInvariant());
                httpRequest.Content.Headers.ContentType = request.ContentType;
            }
        }

        private void SetHeaders(
            IHttpRequest request,
            HttpRequestMessage httpRequest,
            ref HashSet<string> headersOnContentObject)
        {
            foreach (var header in request.Headers)
            {
                if (!headersOnContentObject.Contains(header.Key.ToLowerInvariant()))
                {
                    if (!httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value))
                    {
                        httpRequest.Content?.Headers?.TryAddWithoutValidation(header.Key, header.Value);
                        headersOnContentObject.Add(header.Key.ToLowerInvariant());
                    }
                }
                else
                {
                    this.log.LogDebug("Skipping header already present in the content object",
                        new { header.Key, header.Value });
                }
            }
        }

        private void SetServerSslSecurity(IHttpRequest request, SocketsHttpHandler socketsHttpHandler)
        {
            // Until .NET Core 2.0 this was HttpClientHandler. Starting from .NET Core 2.1 this is SocketsHttpHandler            

            if (request.Options.AllowInsecureSslServer && request.UsesSsl())
            {
                // Until .NET Core 2.0: clientHandler.ServerCertificateCustomValidationCallback += delegate(HttpRequestMessage sender, X509Certificate2 cert, X509Chain chain, SslPolicyErrors error)
                // Starting from .NET Core 2.1:
                socketsHttpHandler.SslOptions.RemoteCertificateValidationCallback += delegate (object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors sslPolicyErrors)                    
                {
                    if (string.IsNullOrEmpty(cert.GetCertHashString())) return false;

                    var sslThumbprint = cert.GetCertHashString().ToLowerInvariant();
                    var configThumbprint = this.config.SslCertThumbprint.ToLowerInvariant();
                    if (sslThumbprint == configThumbprint) return true;

                    this.log.LogError("The remote endpoint is using an unknown/invalid SSL certificate, " +
                                   "the thumbprint of the certificate doesn't match the value in the configuration",
                        new { sslThumbprint, configThumbprint });

                    return false;
                };
            }
        }        
    }
}
