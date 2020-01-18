// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.IoTSolutions.ReverseProxy.Diagnostics;
using Microsoft.Azure.IoTSolutions.ReverseProxy.Runtime;
using Microsoft.Extensions.Logging;
using ReverseProxy;

namespace Microsoft.Azure.IoTSolutions.ReverseProxy
{
    public class ProxyMiddleware
    {        
        private readonly IProxy proxy;
        private readonly ILogger<ProxyMiddleware> log;

        public ProxyMiddleware(
            // ReSharper disable once UnusedParameter.Local
            RequestDelegate next, // Required by ASP.NET
            IConfig config,
            IProxy proxy,
            ILogger<ProxyMiddleware> log)
        {
            this.proxy = proxy;
            this.log = log;
            
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await this.proxy.ProcessAsync(context.Request, context.Response);
                this.log.LogDebug("--------------------------------------------------------------------------------");
            }
            catch (Exception e)
            {
                this.log.LogError("Proxied request failed", new { e });
                context.Response.StatusCode = (int) HttpStatusCode.InternalServerError;

                var buffer = Encoding.UTF8.GetBytes($"Error: {e.Message} [{e.GetType().FullName}]");
                context.Response.Body.Write(buffer, 0, buffer.Length);
            }
        }
    }
}
