using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReverseProxy.Models;
using System;
using System.Collections.Generic;
using System.IO;
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
        public void GetFeatureFromCookieOrHeader_GetsCorrectFeatureFromHeader()
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
        public void GetFeatureFromCookieOrHeader_GetsCorrectFeatureFromCookie()
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
        public void GetFeatureFromCookieOrHeader_FeatureNotPresent()
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
        public void GetUrlFromFeatureConfiguration_NonExistingFeature_ResultsInNull()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();

            // Act
            var result = target.GetUrlFromFeatureConfiguration("XYZ", httpContext.Request);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetUrlFromFeatureConfiguration_NoFeatureSpecified_ResultsInDefaultUrl()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            var randomurl = "http://" + Guid.NewGuid();
            target.FeaturesOld = new Dictionary<string, Dictionary<string, string>>
            {
                { FeaturesManager.DEFAULTFEATURE, new Dictionary<string, string>
                    {
                        { FeaturesManager.DEFAULTURLKEY, randomurl }
                    }
                }
            };
            httpContext.Request.Scheme = "http";
            httpContext.Request.Host = HostString.FromUriComponent(Guid.NewGuid().ToString());

            // Act
            var result = target.GetUrlFromFeatureConfiguration(null, httpContext.Request);

            // Assert
            Assert.Equal(randomurl, result?.toUrl);
        }

        [Fact]
        public void ReadConfig_FeatureWithoutHost_IsAddedToDefaultHost()
        {
            // Act
            target.ReadConfig(Directory.GetCurrentDirectory() + "/TestConfigs/testconfig1.json");

            // Assert
            Assert.True(target.featuresRoot.DefaultHost.Features.ContainsKey(FeaturesManager.DEFAULTFEATURE));
        }

        [Fact]
        public void ReadConfig_PathWithSlash_IsAddedAsTree()
        {
            // Act
            target.ReadConfig(Directory.GetCurrentDirectory() + "/TestConfigs/testconfig1.json");

            // Assert
            var feature = target.featuresRoot.DefaultHost.Features[FeaturesManager.DEFAULTFEATURE];
            Assert.Equal("applications/myapplication", feature.Urls.Children["apps"].Children["myapp"].Value);
        }

        [Fact]
        public void ReadConfig_FeatureWithHostname_IsAddedToHostname()
        {
            // Act
            target.ReadConfig(Directory.GetCurrentDirectory() + "/TestConfigs/testconfig1.json");

            // Assert
            var hostname = target.featuresRoot.Hostnames["otherhostname.local"];
            var feature = hostname.Features[FeaturesManager.DEFAULTFEATURE];
            Assert.Equal("http://otherbackend.local", feature.Urls.Children[FeaturesManager.DEFAULTURLKEY].Value);

        }
    }
}
