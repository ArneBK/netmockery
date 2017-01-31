using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using System.Net;

namespace netmockery
{
    public class TestCaseHttpRequest : IHttpRequestWrapper
    {
        private string path;
        private string querystring;
        private HeaderDictionary headerDictionary = new HeaderDictionary();

        public TestCaseHttpRequest(string path, string querystring)
        {
            this.path = path;
            this.querystring = querystring;
        }

        public IHeaderDictionary Headers => headerDictionary;

        public PathString Path => new PathString(path);

        public QueryString QueryString => new QueryString(querystring);
    }

    public class TestCaseHttpResponse : IHttpResponseWrapper
    {
        MemoryStream memoryStream = new MemoryStream();
        string writtenContent;
        Encoding writtenEncoding;
        string contentType;
        int statusCode;
        bool writeAsyncCalled = false;

        public Stream Body => memoryStream;

        public string ContentType
        {
            set
            {
                contentType = value;
            }
        }

        public HttpStatusCode HttpStatusCode
        {
            set
            {
                statusCode = (int)value;
            }
        }

        async public Task WriteAsync(string content, Encoding encoding)
        {
            Debug.Assert(writeAsyncCalled == false);
            writtenContent = content;
            writtenEncoding = encoding;
            writeAsyncCalled = true;

            await Task.Yield();
        }

        public string GetWrittenResponseAsString()
        {
            if (! writeAsyncCalled)
            {
                throw new InvalidOperationException("This is a binary response");
            }

            return writtenContent;
        }
    }


    //TODO: Refactoring needed. Smell: RequestInfo is created in two different places (search for new RequestInfo)
    public class NetmockeryTestCase
    {
        public string Name;
        public string RequestPath;
        public string QueryString;
        public string RequestBody;

        public string ExpectedRequestMatcher;
        public string ExpectedResponseCreator;

        public string ExpectedContentType;
        public string ExpectedCharSet;
        public string ExpectedResponseBody;

        public bool NeedsResponseBody
        {
            get
            {
                return (new[] { ExpectedResponseBody, ExpectedContentType, ExpectedCharSet }).Any(val => val != null);
            }
        }

        public bool HasExpectations
        {
            get
            {
                return (new[] { ExpectedResponseBody, ExpectedRequestMatcher, ExpectedResponseCreator, ExpectedContentType, ExpectedCharSet }).Any(val => val != null);
            }
        }


        public bool Evaluate(string requestMatcher, string responseCreator, string responseBody, string contentType, string charset, out string message)
        {
            Debug.Assert(responseBody != null || !NeedsResponseBody);
            Debug.Assert(contentType != null || !NeedsResponseBody);
            Debug.Assert(charset != null || !NeedsResponseBody);

            Debug.Assert(requestMatcher != null);
            Debug.Assert(responseCreator != null);
            message = null;

            if (ExpectedRequestMatcher != null && ExpectedRequestMatcher != requestMatcher)
            {
                message = $"Expected request matcher: {ExpectedRequestMatcher}\nActual: {requestMatcher}";
                return false;
            }

            if (ExpectedResponseCreator != null && ExpectedResponseCreator != responseCreator)
            {
                message = $"Expected response creator: {ExpectedResponseCreator}\nActual: {responseCreator}";
                return false;
            }

            if (ExpectedResponseBody != null && ExpectedResponseBody != responseBody)
            {
                message = $"Expected response body:\n{ExpectedResponseBody}\n\nActual response body:\n{responseBody}";
                return false;
            }

            if (ExpectedContentType != null && ExpectedContentType != contentType)
            {
                message = $"Expected contenttype: '{ExpectedContentType}'\nActual: '{contentType}'";
                return false;
            }

            if (ExpectedCharSet != null && ExpectedCharSet != charset)
            {
                message = $"Expected charset: '{ExpectedCharSet}'\nActual: '{charset}'";
                return false;
            }

            Debug.Assert(message == null);
            return true;
        }

        async public Task<NetmockeryTestCaseResult> ExecuteAgainstHttpClientAsync(HttpClient client, string requestUrl)
        {
            var retval = new NetmockeryTestCaseResult { TestCase = this };
            if (QueryString != null)
            {
                requestUrl += QueryString;
            }
            HttpResponseMessage responseMessage;
            if (RequestBody != null)
            {
                responseMessage = await client.PostAsync(requestUrl, new ByteArrayContent(Encoding.UTF8.GetBytes(RequestBody)));
            }
            else
            {
                responseMessage = await client.GetAsync(requestUrl);
            }
            var body = await responseMessage.Content.ReadAsStringAsync();

            string message;
            var requestMatcher = "";
            if (responseMessage.Headers.Contains("X-Netmockery-RequestMatcher"))
            {
                requestMatcher = responseMessage.Headers.GetValues("X-Netmockery-RequestMatcher").ElementAt(0);
            }
            var responseCreator = "";
            if (responseMessage.Headers.Contains("X-Netmockery-ResponseCreator"))
            {
                responseCreator = responseMessage.Headers.GetValues("X-Netmockery-ResponseCreator").ElementAt(0);
            }
            var contentType = "";
            var charset = "";
            if (responseMessage.Content.Headers.ContentType != null)
            {
                contentType = responseMessage.Content.Headers.ContentType.MediaType;
                charset = responseMessage.Content.Headers.ContentType.CharSet;
            }

            if (Evaluate(requestMatcher, responseCreator, body, contentType, charset, out message))
            {
                retval.SetSuccess();
            }
            else
            {
                retval.SetFailure(message);
            }
            return retval;

        }

        async public Task<NetmockeryTestCaseResult> ExecuteAgainstUrlAsync(string url)
        {
            return await ExecuteAgainstHttpClientAsync(new HttpClient(), url + RequestPath);
        }

        private const string ERROR_NOMATCHING_ENDPOINT = "No endpoint matches request path";
        private const string ERROR_ENDPOINT_HAS_NO_MATCH = "Endpoint has no match for request";


        async public Task<NetmockeryTestCaseResult> ExecuteAsync(EndpointCollection endpointCollection, bool handleErrors=true, DateTime? now=null)
        {
            Debug.Assert(endpointCollection != null);

            var testResult = new NetmockeryTestCaseResult { TestCase = this };
            try
            {
                var endpoint = endpointCollection.Resolve(RequestPath);
                if (endpoint == null)
                {
                    return testResult.SetFailure(ERROR_NOMATCHING_ENDPOINT);
                }
                testResult.EndpointName = endpoint.Name;

                var matcher_and_creator = endpoint.Resolve(new PathString(RequestPath), new QueryString(QueryString), RequestBody ?? "", null);
                if (matcher_and_creator == null)
                {
                    return testResult.SetFailure(ERROR_ENDPOINT_HAS_NO_MATCH);
                }
                if (!HasExpectations)
                {
                    return testResult.SetFailure("Test case has no expectations");
                }

                var responseCreator = matcher_and_creator.ResponseCreator;
                string responseBody = null;
                string charset = "";
                string contenttype = "";
                if (NeedsResponseBody)
                {
                    var simpleResponseCreator = responseCreator as SimpleResponseCreator;
                    if (simpleResponseCreator == null)
                    {
                        return testResult.SetFailure($"Response creator {responseCreator.ToString()} not supported by test framework");
                    }

                    var requestInfo = new RequestInfo
                    {
                        EndpointDirectory = endpoint.Directory,
                        Headers = null,
                        RequestPath = RequestPath,
                        QueryString = QueryString,
                        RequestBody = RequestBody
                    };
                    if (now != null)
                    {
                        requestInfo.SetStaticNow(now.Value);
                    }
                    responseBody = simpleResponseCreator.GetBodyAndExecuteReplacements(requestInfo);
                    contenttype = simpleResponseCreator.ContentType ?? "";
                    charset = simpleResponseCreator.Encoding.WebName;
                }
                string message;
                if (Evaluate(matcher_and_creator.RequestMatcher.ToString(), matcher_and_creator.ResponseCreator.ToString(), responseBody, contenttype, charset, out message))
                {
                    return testResult.SetSuccess();
                }
                else
                {
                    return testResult.SetFailure(message);
                }
            }
            catch (Exception exception)
            {
                if (!handleErrors) throw;
                return testResult.SetException(exception);
            }
        }

        public Tuple<string, string> GetResponse(EndpointCollection endpointCollection, DateTime? now)
        {
            var endpoint = endpointCollection.Resolve(RequestPath);
            if (endpoint == null)
            {
                return Tuple.Create((string)null, ERROR_NOMATCHING_ENDPOINT);
            }
            var matcher_and_creator = endpoint.Resolve(new PathString(RequestPath), new QueryString(QueryString), RequestBody ?? "", null);
            if (matcher_and_creator != null)
            {
                var responseCreator = matcher_and_creator.ResponseCreator as SimpleResponseCreator;
                if (responseCreator == null)
                {
                    return Tuple.Create((string)null, $"This response creator is not supported by test framework: {matcher_and_creator.ResponseCreator.ToString()}");
                }

                var requestInfo = new RequestInfo
                {
                    EndpointDirectory = endpoint.Directory,
                    Headers = null,
                    QueryString = QueryString,
                    RequestBody = RequestBody,
                    RequestPath = RequestPath
                };
                if (now != null)
                {
                    requestInfo.SetStaticNow(now.Value);
                }
                var responseBody = responseCreator.GetBodyAndExecuteReplacements(requestInfo);
                return Tuple.Create(responseBody, (string)null);
            }
            else
            {
                return Tuple.Create((string)null, ERROR_ENDPOINT_HAS_NO_MATCH);
            }

        }
    }

    public class NetmockeryTestCaseResult
    {
        private bool _ok;
        private Exception _exception;
        private string _message;

        public bool OK => _ok;
        public bool Error => !_ok;
        public Exception Exception => _exception;
        public string Message => _message;
        public NetmockeryTestCase TestCase;
        public string EndpointName;
        public int ResponseIndex;

        public NetmockeryTestCaseResult SetSuccess()
        {
            _ok = true;
            return this;
        }

        public NetmockeryTestCaseResult SetFailure(string message)
        {
            _ok = false;
            _message = message;
            return this;
        }

        public NetmockeryTestCaseResult SetException(Exception e)
        {
            Debug.Assert(e != null);
            SetFailure("Exception");
            _exception = e;
            return this;
        }

        private static string indent(string s)
        {
            Debug.Assert(s != null);
            return "    " + s.Replace(Environment.NewLine, Environment.NewLine + "    ");
        }

        public string ResultAsString
        {
            get
            {
                var shortstatus = "";
                if (OK)
                {
                    shortstatus = "OK";
                }
                if (Error)
                {
                    shortstatus = "Fail";
                }
                if (Exception != null)
                {
                    shortstatus = "Error";
                }

                if (Error)
                {
                    var retval = $"{shortstatus}\n{indent(Message ?? "")}";
                    if (Exception != null)
                    {
                        retval += "\n" + indent(Exception.ToString());
                    }
                    return retval;
                }
                else
                {
                    return shortstatus;
                }
            }
        }
    }
}
