﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using netmockery;
using System.Diagnostics;

namespace UnitTests
{
    public class TestInitFromJSON
    {
        public const string ENDPOINTJSON = @"
{
    'name': 'foo',
    'pathregex': '^/foo/$',
    'responses': [
        {
            'match': {'regex': 'test'},
            'file': 'content.txt',
            'contenttype': 'text/plain',
            'delay': 10
        },
        {
            'match': {},
            'script': 'myscript.csscript',
            'contenttype': 'text/xml',
            'replacements': [
                {'search': 'a', 'replace': 'b'},
                {'search': 'foo', 'replace': 'bar'}
            ]
        }
    ]
}
";

        [Fact]
        public void SimpleEndpointAttributes()
        {
            var endpoint = JSONReader.ReadEndpoint(ENDPOINTJSON, "p:\\ath\\to\\endpoint\\directory", globalDefaults: null);
            Assert.Equal("foo", endpoint.Name);
            Assert.Equal("^/foo/$", endpoint.PathRegex);
        }

        private Tuple<RequestMatcher, ResponseCreator> ParseResponse(string json)
        {
            var endpoint = JSONReader.ReadEndpoint("{'name': 'foo', 'pathregex': 'foo', 'responses': [" + json + "]}", "r:\\oot\\directory", globalDefaults: null);
            var responses = endpoint.Responses.ToArray();
            Debug.Assert(responses.Length == 1);
            return responses[0];
        }

        [Fact]
        public void Responses()
        {
            var endpoint = JSONReader.ReadEndpoint(ENDPOINTJSON, "p:\\ath\\to\\endpoint\\directory", globalDefaults: null);
            var responses = endpoint.Responses.ToArray();
            Assert.Equal(2, responses.Length);

            var rm0 = responses[0].Item1 as RegexMatcher;
            var rc0 = responses[0].Item2 as FileResponse;

            Assert.NotNull(rm0);
            Assert.NotNull(rc0);

            Assert.Equal("p:\\ath\\to\\endpoint\\directory\\content.txt", rc0.Filename);
            Assert.Equal("text/plain", rc0.ContentType);
            Assert.Equal(10, rc0.Delay);
            Assert.Equal(0, rc0.Replacements.Length);

            var rm1 = responses[1].Item1 as AnyMatcher;
            var rc1 = responses[1].Item2 as FileDynamicResponseCreator;

            Assert.NotNull(rm1);
            Assert.NotNull(rc1);
            Assert.Equal("p:\\ath\\to\\endpoint\\directory\\myscript.csscript", rc1.Filename);
            Assert.Equal("text/xml", rc1.ContentType);
            Assert.Equal(0, rc1.Delay);
            Assert.Equal(2, rc1.Replacements.Length);
        }

        [Fact]
        public void DeserializeXPathRequestMatcher()
        {
            var response = ParseResponse("{'match': {'xpath': 'foo', 'namespaces': [{'prefix': 'foo', 'ns': 'urn:foo'}]}, 'file': 'foo.txt'}");
            var xpathMatcher = response.Item1 as XPathMatcher;
            Assert.NotNull(xpathMatcher);
            Assert.Equal("foo", xpathMatcher.XPathExpresssion);
            Assert.Equal(1, xpathMatcher.Namespaces.Length);
            Assert.Equal("urn:foo", xpathMatcher.Namespaces[0]);
        }

        [Fact]
        public void DeserializeRegExRequestMatcher()
        {
            var response = ParseResponse("{'match': {'regex': 'foobar'}, 'file': 'foo.txt'}");
            var regexMatcher = response.Item1 as RegexMatcher;
            Assert.NotNull(regexMatcher);
            Assert.Equal("foobar", regexMatcher.Expression);
        }

        [Fact]
        public void DeserializeCompiledCreator()
        {
            var response = ParseResponse("{'match': {'regex': 'foobar'}, 'assembly': 'foo.dll', 'class': 'Foo', 'method': 'Bar'}");
            var responseCreator = response.Item2 as AssemblyResponseCreator;
            Assert.NotNull(responseCreator);
            Assert.Equal("r:\\oot\\directory\\foo.dll", responseCreator.AssemblyFilename);
            Assert.Equal("Foo", responseCreator.ClassName);
            Assert.Equal("Bar", responseCreator.MethodName);
        }

        [Fact]
        public void DeserializeForwardCreator()
        {
            var response = ParseResponse("{'match': {}, 'forward': 'http://foo.bar'}");
            var responseCreator = response.Item2 as ForwardResponseCreator;
            Assert.NotNull(responseCreator);
            Assert.Equal("http://foo.bar", responseCreator.Url);
            Assert.Null(responseCreator.ProxyUrl);
            Assert.Null(responseCreator.StripPath);
        }

        [Fact]
        public void DeserializeForwardCreatorWithProxyAndStripPath()
        {
            var response = ParseResponse("{'match': {}, 'forward': 'http://foo.bar', 'proxy': 'http://localhost:1234', 'strippath': 'foo'}");
            var responseCreator = response.Item2 as ForwardResponseCreator;
            Assert.NotNull(responseCreator);
            Assert.Equal("http://foo.bar", responseCreator.Url);
            Assert.Equal("http://localhost:1234", responseCreator.ProxyUrl);
            Assert.Equal("foo", responseCreator.StripPath);
        }

        [Fact]
        public void DeserializeLiteralResponse()
        {
            var response = ParseResponse("{'match': {}, 'literal': 'Hello world', 'contenttype': 'text/plain'}");
            var responseCreator = response.Item2 as LiteralResponse;
            Assert.NotNull(responseCreator);
            Assert.Equal("Hello world", responseCreator.GetBody(null));
            Assert.Equal("text/plain", responseCreator.ContentType);
            Assert.Equal("Literal string: Hello world", responseCreator.ToString());
        }
    }
}
