using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReverseProxy.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace ProxyAgent.Test.Models
{
    public class FeaturesManagerTest
    {
        private readonly FeaturesManager target;
        public FeaturesManagerTest(ITestOutputHelper log)
        {
            var services = new ServiceCollection()
                .AddLogging((builder) => builder.AddXUnit(log))
                .AddTransient<FeaturesManager>();
                

            var provider = services.BuildServiceProvider();
            this.target = provider.GetRequiredService<FeaturesManager>();
        }

        [Fact]
        public void FeaturesManager_GetFeatureFromCookieOrHeader_GetsCorrectFeatureFromHeader()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var randomfeaturename = Guid.NewGuid().ToString();
            httpContext.Request.Headers.Add(FeaturesManager.HTTPHEADER_FEATURE, randomfeaturename);
            
            // Act
            var result = target.GetFeatureFromCookieOrHeader(httpContext.Request);

            // Assert
            Assert.Equal(randomfeaturename, result.feature);
            Assert.Equal(FeatureAvailability.Header, result.featureAvailability);
        }

        [Fact]
        public void FeaturesManager_GetFeatureFromCookieOrHeader_GetsCorrectFeatureFromCookie()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var randomfeaturename = Guid.NewGuid().ToString();
            httpContext.Request.Headers.Add("Cookie", new CookieHeaderValue(FeaturesManager.COOKIE_FEATURE, randomfeaturename).ToString());

            // Act
            var result = target.GetFeatureFromCookieOrHeader(httpContext.Request);

            // Assert
            Assert.Equal(randomfeaturename, result.feature);
            Assert.Equal(FeatureAvailability.Cookie, result.featureAvailability);
        }

        [Fact]
        public void FeaturesManager_GetFeatureFromCookieOrHeader_FeatureNotPresent()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();                       

            // Act
            var result = target.GetFeatureFromCookieOrHeader(httpContext.Request);

            // Assert
            Assert.Null(result.feature);
            Assert.Equal(FeatureAvailability.NotPresent, result.featureAvailability);
        }

        [Fact]
        public void FeaturesManager_GetUrlFromFeatureConfiguration_XYZ()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();

            // Act
            var result = target.GetUrlFromFeatureConfiguration("XYZ", httpContext.Request);
        }
    }
}
