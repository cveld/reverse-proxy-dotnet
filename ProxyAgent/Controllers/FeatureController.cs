using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.IoTSolutions.ReverseProxy.Models;
using ReverseProxy.Models;

namespace ProxyAgent.Controllers
{
    [Route("feature")]
    public class FeatureController : Controller
    {
        private readonly FeaturesManager featuresManager;

        public FeatureController(FeaturesManager featuresManager)
        {
            this.featuresManager = featuresManager;
        }        
        public IActionResult Index()
        {            
            var result = featuresManager.Features.Keys;
            var currentFeature = featuresManager.GetFeatureFromCookieOrHeader(Request);
            ViewBag.Features = result.Select(c => c);
            ViewBag.CurrentFeature = currentFeature;
            return View();
        }

        [HttpGet("{feature}")]
        public IActionResult Index(string feature)
        {
            if (featuresManager.Features.ContainsKey(feature)) {
                Response.Cookies.Append("feature", feature);
                return Ok();
            } 
            return NotFound();
        }

        [HttpGet("current")]
        public IActionResult CurrentFeature()
        {
            var currentFeature = featuresManager.GetFeatureFromCookieOrHeader(Request);
            ViewBag.CurrentFeature = currentFeature;
            return View();
        }
    }
}