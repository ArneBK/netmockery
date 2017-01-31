using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using netmockery;
using Xunit;
using System.Net;

namespace UnitTests
{
    public class TestDefaults : IDisposable
    {
        DirectoryCreator directoryCreator = new DirectoryCreator();
        EndpointCollection endpointCollection;

        public void InitializeEndpointCollectionWithoutDefaults()
        {
            var jsonEndpoint1 = new JSONEndpoint
            {
                name = "foobar",
                pathregex = "foobar",
                responses = new[] {
                    new JSONResponse {
                        match = new JSONRequestMatcher(),
                        file = "myfile.xml"
                    }
                }
            };

            directoryCreator.AddFile("endpoint1\\endpoint.json", JsonConvert.SerializeObject(jsonEndpoint1));

            endpointCollection = EndpointCollectionReader.ReadFromDirectory(directoryCreator.DirectoryName);
        }

        public void InitializeEndpointCollectionWithGlobalDefaultsOnly()
        {
            var jsonEndpoint1 = new JSONEndpoint
            {
                name = "foobar",
                pathregex = "foobar",
                responses = new[] {
                    new JSONResponse {
                        match = new JSONRequestMatcher(),
                        file = "myfile.xml"
                    }
                }
            };

            var jsonEndpoint2 = new JSONEndpoint
            {
                name = "baz",
                pathregex = "baz",
                responses = new[] {
                    new JSONResponse {
                        match = new JSONRequestMatcher(),
                        file = "myfile.xml",
                        contenttype = "text/xml",
                        charset = "utf-8"
                    }
                }
            };

            var jsonEndpoint3 = new JSONEndpoint
            {
                name = "lorem",
                pathregex = "ipsum",
                responses = new[] {
                    new JSONResponse {
                        match = new JSONRequestMatcher(),
                        file = "myfile.xml",
                        contenttype = "text/xml",
                        httpStatusCode = "200"
                    }
                }
            };

            directoryCreator.AddFile("defaults.json", JsonConvert.SerializeObject(new JSONDefaults { charset = "ascii", contenttype = "application/xml" }));
            directoryCreator.AddFile("endpoint1\\endpoint.json", JsonConvert.SerializeObject(jsonEndpoint1));
            directoryCreator.AddFile("endpoint2\\endpoint.json", JsonConvert.SerializeObject(jsonEndpoint2));
            directoryCreator.AddFile("endpoint3\\endpoint.json", JsonConvert.SerializeObject(jsonEndpoint3));

            endpointCollection = EndpointCollectionReader.ReadFromDirectory(directoryCreator.DirectoryName);
        }

        public void InitializeEndpointCollectionWithGlobalAndEndpointDefaults()
        {
            var jsonEndpoint1 = new JSONEndpoint
            {
                name = "noendpointdefaults",
                pathregex = "noendpointdefaults",
                responses = new[] {
                    new JSONResponse {
                        match = new JSONRequestMatcher(),
                        file = "myfile.xml"
                    }
                }
            };

            var jsonEndpoint2 = new JSONEndpoint
            {
                name = "endpointdefaults",
                pathregex = "endpointdefaults",
                responses = new[] {
                    new JSONResponse {
                        match = new JSONRequestMatcher(),
                        file = "myfile.xml",
                    }
                }
            };
            var globalDefaults = new JSONDefaults { charset = "ascii", contenttype = "application/xml" };
            var endpointDefaults = new JSONDefaults { charset = "UTF-7", contenttype = "text/plain" };

            directoryCreator.AddFile("defaults.json", JsonConvert.SerializeObject(globalDefaults));
            directoryCreator.AddFile("endpoint1\\endpoint.json", JsonConvert.SerializeObject(jsonEndpoint1));
            directoryCreator.AddFile("endpoint2\\endpoint.json", JsonConvert.SerializeObject(jsonEndpoint2));
            directoryCreator.AddFile("endpoint2\\defaults.json", JsonConvert.SerializeObject(endpointDefaults));

            endpointCollection = EndpointCollectionReader.ReadFromDirectory(directoryCreator.DirectoryName);

        }

        public void Dispose()
        {
            directoryCreator.Dispose();
        }

        [Fact]
        public void EndpointDefaultsAreApplied()
        {
            InitializeEndpointCollectionWithGlobalAndEndpointDefaults();

            var responseCreator = endpointCollection.Get("endpointdefaults").Responses.Single().Item2 as SimpleResponseCreator;
            Assert.Equal("text/plain", responseCreator.ContentType);
            Assert.Equal("utf-7", responseCreator.Encoding.WebName);
        }

        [Fact]
        public void GlobalDefaultsAreApplied()
        {
            InitializeEndpointCollectionWithGlobalDefaultsOnly();

            var endpoint = endpointCollection.Get("foobar");
            var responseCreator = endpoint.Responses.Single().Item2 as SimpleResponseCreator;
            Assert.Equal("application/xml", responseCreator.ContentType);
            Assert.Equal("us-ascii", responseCreator.Encoding.WebName);            
        }

        [Fact]
        public void DefaultsCanBeOverridden()
        {
            InitializeEndpointCollectionWithGlobalDefaultsOnly();

            var endpoint = endpointCollection.Get("baz");
            var responseCreator = endpoint.Responses.Single().Item2 as SimpleResponseCreator;
            Assert.Equal("text/xml", responseCreator.ContentType);
            Assert.Equal("utf-8", responseCreator.Encoding.WebName);
        }

        [Fact]
        public void ValidateDefaultEncodingWithoutDefaults()
        {
            InitializeEndpointCollectionWithoutDefaults();

            var responseCreator = endpointCollection.Get("foobar").Responses.Single().Item2 as SimpleResponseCreator;
            Assert.Equal("utf-8", responseCreator.Encoding.WebName);
        }

        [Fact]
        public void ValidateDefaultHttpResponseCode()
        {
            InitializeEndpointCollectionWithGlobalDefaultsOnly();

            var responseCreator = endpointCollection.Get("lorem").Responses.Single().Item2 as SimpleResponseCreator;
            Assert.Equal(HttpStatusCode.OK, responseCreator.HttpStatusCode);
        }
    }
}
