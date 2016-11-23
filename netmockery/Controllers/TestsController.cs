﻿using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
using System.Text;

namespace netmockery.Controllers
{
    public class WebTestRunner : TestRunner
    {
        StringBuilder response = new StringBuilder();

        public WebTestRunner(EndpointCollection endpointCollection) : base(endpointCollection)
        { 
        }

        public override string ToString() => response.ToString();

        public override void WriteBeginTest(int index, NetmockeryTestCase testcase)
        {
            response.Append($"{index} {testcase.Name} ");
        }

        public override void WriteError(string s)
        {
            response.AppendLine($"ERROR: {s}");
        }

        public override void WriteResponse(string s)
        {
            response.Append(s);
        }

        public override void WriteResult(NetmockeryTestCaseResult result)
        {
            response.AppendLine(result.ResultAsString);
        }

        public override void WriteSummary(int errors)
        {
            response.AppendLine($"Total: {Tests.Count()} Errors: {errors}");
        }
    }
    public class TestsController : Controller
    {
        private EndpointCollection endpoints;
        private WebTestRunner testRunner;
        private HttpContext httpContext;

        public TestsController(EndpointCollection endpoints, IHttpContextAccessor contextAccessor)
        {
            this.endpoints = endpoints;

            // contextAccessor is injected so that Request is available inside constructor (via TestAgainstUrl property)
            httpContext = contextAccessor.HttpContext;


            if (TestRunner.HasTestSuite(endpoints.SourceDirectory))
            {
                testRunner = new WebTestRunner(endpoints);
                if (TestAgainstUrl)
                {
                    testRunner.Url = "http://localhost:5000";
                }
            }            
        }

        public ActionResult Index()
        {            
            return View(testRunner);
        }

        const string TESTURLCOOKIE = "testurl";

        /* See comment in constructor as to why injected HttpContext is used */
        private bool TestAgainstUrl => httpContext.Request.Cookies.ContainsKey(TESTURLCOOKIE);
                

        public ActionResult ToggleMode()
        {
            if (TestAgainstUrl)
            {
                Response.Cookies.Delete(TESTURLCOOKIE);
            }
            else
            {
                Response.Cookies.Append(TESTURLCOOKIE, "yes");
            }
            return RedirectToAction("Index");
        }

        public ActionResult RunAll()
        {
            testRunner.TestAll();
            return Content(testRunner.ToString());
        }

        public ActionResult Run(int index)
        {
            testRunner.ExecuteTestAndOutputResult(index);
            return Content(testRunner.ToString());
        }

        public ActionResult ViewResponse(int index)
        {
            testRunner.ShowResponse(index);
            return Content(testRunner.ToString());
        }

        public ActionResult ExpectedResponseBody(int index)
        {
            var test = testRunner.Tests.ElementAt(index);
            return Content(test.ExpectedResponseBody);
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (testRunner == null)
            {
                context.Result = View("NoTests", null);
            }
        }
    }
}
