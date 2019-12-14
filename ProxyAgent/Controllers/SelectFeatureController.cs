using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.IoTSolutions.ReverseProxy.Models;

namespace ProxyAgent.Controllers
{
    [Route("selectfeature")]
    public class SelectFeatureController : Controller
    {
        private readonly FeaturesManager featuresManager;

        public SelectFeatureController(FeaturesManager featuresManager)
        {
            this.featuresManager = featuresManager;
        }        
        public IActionResult Index()
        {            
            var result = featuresManager.Features.Keys;            
            ViewBag.Features = result.Select(c => c);
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
    }
}