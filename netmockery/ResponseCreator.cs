using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net;

namespace netmockery
{    
    public interface IHttpResponseWrapper
    {
        string ContentType { set; }
        Stream Body { get; }
        HttpStatusCode HttpStatusCode { set; }

        Task WriteAsync(string content, Encoding encoding);
    }

    public interface IHttpRequestWrapper
    {
        PathString Path { get; }
        QueryString QueryString { get; }
        IHeaderDictionary Headers { get; }
    }

    public class HttpRequestWrapper : IHttpRequestWrapper
    {
        HttpRequest httpRequest;

        public HttpRequestWrapper(HttpRequest httpRequest)
        {
            Debug.Assert(httpRequest != null);
            this.httpRequest = httpRequest;
        }
        public IHeaderDictionary Headers => httpRequest.Headers;
        public PathString Path => httpRequest.Path;
        public QueryString QueryString => httpRequest.QueryString;
    }

    public class HttpResponseWrapper : IHttpResponseWrapper
    {
        private HttpResponse httpResponse;
        public HttpResponseWrapper(HttpResponse httpResponse)
        {
            Debug.Assert(httpResponse != null);
            this.httpResponse = httpResponse;
        }
        public Stream Body
        {
            get
            {
                return httpResponse.Body;
            }
        }

        public string ContentType
        {
            set
            {
                httpResponse.ContentType = value;
            }
        }

        public HttpStatusCode HttpStatusCode
        {
            set
            {
                httpResponse.StatusCode = (int)value;
            }
        }

        async public Task WriteAsync(string content, Encoding encoding)
        {
            await httpResponse.WriteAsync(content, encoding);
        }
    }

    public interface IResponseCreatorWithFilename
    {
        string Filename { get; }
    }

    public class RequestInfo
    {
        private DateTime _now = DateTime.MinValue;

        public string RequestPath;
        public string QueryString;
        public string RequestBody;
        public IHeaderDictionary Headers;
        public string EndpointDirectory;

        public DateTime GetNow() => _now == DateTime.MinValue ? DateTime.Now : _now;

        public void SetStaticNow(DateTime now)
        {
            _now = now;
        }
    }

    public class BodyReplacement
    {
        public string SearchTerm;
        public string ReplacementTerm;
    }

    public abstract class ResponseCreator
    {
        public int Delay { get; set; } = 0;
        public abstract Task<byte[]> CreateResponseAsync(IHttpRequestWrapper request, byte[] requestBody, IHttpResponseWrapper response, string endpointDirectory);
    }

    public abstract class SimpleResponseCreator : ResponseCreator
    {
        public override async Task<byte[]> CreateResponseAsync(IHttpRequestWrapper request, byte[] requestBody, IHttpResponseWrapper response, string endpointDirectory)
        {
            var responseBody = GetBodyAndExecuteReplacements(new RequestInfo
            {
                RequestPath = request.Path.ToString(),
                QueryString = request.QueryString.ToString(),
                RequestBody = Encoding.UTF8.GetString(requestBody),                
                Headers = request.Headers,
                EndpointDirectory = endpointDirectory
            });
            if (ContentType != null)
            {
                var contenttype = ContentType;
                contenttype += $"; charset={Encoding.WebName}";
                response.ContentType = contenttype;
                response.HttpStatusCode = HttpStatusCode;
            }
            await response.WriteAsync(responseBody, Encoding);
            return Encoding.GetBytes(responseBody);
        }
        public string ContentType { get; set; }
        public abstract string GetBody(RequestInfo requestInfo);
        public Encoding Encoding { get; set; } = Encoding.UTF8;
        public HttpStatusCode HttpStatusCode { get; set; } = HttpStatusCode.OK;

        public BodyReplacement[] Replacements = new BodyReplacement[0];

        public string GetBodyAndExecuteReplacements(RequestInfo requestInfo)
        {
            var retval = GetBody(requestInfo);

            Debug.Assert(Replacements != null);
            if (Replacements.Length > 0)
            {
                foreach (var bodyReplacement in Replacements)
                {
                    retval = Regex.Replace(retval, bodyReplacement.SearchTerm, bodyReplacement.ReplacementTerm);
                }
            }
            return retval;
        }

    }


    public class LiteralResponse : SimpleResponseCreator
    {
        private string _body;

        public string Body => _body;

        public LiteralResponse(string body)
        {
            _body = body;
        }

        public override string GetBody(RequestInfo requestInfo) => _body;

        public override string ToString() => $"Literal string: {Body}";
    }

    public class FileResponse : SimpleResponseCreator, IResponseCreatorWithFilename
    {
        private string _filename;

        public FileResponse(string filename)
        {
            _filename = filename;
        }

        public string Filename => _filename;

        public override string GetBody(RequestInfo requestInfo) => File.ReadAllText(_filename);

        public override string ToString() => $"File {Path.GetFileName(_filename)}";
    }

}
