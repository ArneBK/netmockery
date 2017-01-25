using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;


namespace netmockery
{
    public abstract class TestRunner
    {
        private IEnumerable<NetmockeryTestCase> testcases;
        protected EndpointCollection endpointCollection;
        protected HashSet<Tuple<string, int>> responsesCoveredByTests = new HashSet<Tuple<string, int>>();
        private DateTime? now;

        public string Url { get; set; }

        public DateTime? Now => now;

        public TestRunner(EndpointCollection endpointCollection)
        {
            this.endpointCollection = endpointCollection;
            Debug.Assert(HasTestSuite(endpointCollection.SourceDirectory));
            testcases = ReadFromDirectory(endpointCollection.SourceDirectory);
            SetStaticTimeIfConfigured(endpointCollection.SourceDirectory);
        }

        public IEnumerable<NetmockeryTestCase> Tests => testcases;

        public void SetStaticTimeIfConfigured(string directory)
        {
            if (File.Exists(now_txt_filename(directory)))
            {
                var contents = File.ReadAllText(now_txt_filename(directory));
                now = DateTime.ParseExact(contents, "yyyy-MM-dd HH:mm:ss", null);
            }
        }


        static public bool HasTestSuite(string directory) => File.Exists(tests_json_filename(directory));

        static private string tests_json_filename(string directory) => Path.Combine(tests_directory(directory), "tests.json");
        static private string now_txt_filename(string directory) => Path.Combine(tests_directory(directory), "now.txt");
        static private string tests_directory(string directory) => Path.Combine(directory, "tests");


        public static IEnumerable<NetmockeryTestCase> ReadFromDirectory(string directory)
        {
            var jsonTests = JsonConvert.DeserializeObject<List<JSONTest>>(File.ReadAllText(tests_json_filename(directory)));
            return from jsontest in jsonTests select jsontest.Validated().CreateTestCase(tests_directory(directory));
        }

        
        public void TestAll(bool stopAfterFirstFailure, bool outputCoverage)
        {
            var errors = 0;
            var index = 0;
            foreach (var test in testcases)
            {
                var result = ExecuteTestAndOutputResult(index++, test);
                if (result.Error)
                {
                    errors++;
                }

                if (result.Error && stopAfterFirstFailure)
                {
                    break;
                }
            }
            WriteSummary(errors);

            if (outputCoverage)
            {
                WriteCoverage();
            }
        }

        public void ListAllTestNames()
        {
            var index = 0;
            foreach (var test in testcases)
            {
                WriteBeginTest(index++, test);
                WriteLine("");
            }
        }

        public void WriteCoverage()
        {
            var ci = GetCoverageInfo();
            Debug.Assert(ci != null);

            WriteLine("");
            WriteLine($"Coverage: {ci.EndpointsCovered.Length} of {ci.EndpointsCovered.Length + ci.EndpointsNotCovered.Length} endpoints");
            if (ci.EndpointsNotCovered.Length > 0)
            {
                WriteLine("Endpoints not covered:");
                foreach (var endpointName in ci.EndpointsNotCovered)
                {
                    WriteLine(endpointName);
                }
            }
        }

        public NetmockeryTestCaseResult ExecuteTestAndOutputResult(int index)
        {
            return ExecuteTestAndOutputResult(index, testcases.ElementAt(index));
        }

        public NetmockeryTestCaseResult ExecuteTestAndOutputResult(int index, NetmockeryTestCase test)
        {
            WriteBeginTest(index, test);
            
            var result = Url != null ? test.ExecuteAgainstUrlAsync(Url).Result : test.ExecuteAsync(endpointCollection, now: now).Result;
            WriteResult(result);

            responsesCoveredByTests.Add(Tuple.Create(result.EndpointName, result.ResponseIndex));

            return result;
        }

        public void ShowResponse(int index)
        {
            var testCase = testcases.ElementAt(index);
            var response = testCase.GetResponse(endpointCollection, Now);
            if (response.Item2 != null)
            {
                WriteError(response.Item2);
            }
            else
            {
                WriteResponse(response.Item1);
            }
        }
        public CoverageInfo GetCoverageInfo()
        {
            var allEndpoints = new HashSet<string>(from endpoint in endpointCollection.Endpoints select endpoint.Name);
            var coveredEndpoints = new HashSet<string>(from tuple2 in responsesCoveredByTests select tuple2.Item1);

            var allResponseRules = new HashSet<string>();
            foreach (var endpoint in endpointCollection.Endpoints)
            {
                var i = 0;
                foreach (var tuple2 in endpoint.Responses)
                {
                    allResponseRules.Add($"{endpoint.Name}#{i++}");
                }
            }

            var coveredResponseRules = new HashSet<string>(from tuple2 in responsesCoveredByTests select $"{tuple2.Item1}#{tuple2.Item2}");

            return new CoverageInfo
            {
                EndpointsCovered = coveredEndpoints.ToArray(),
                EndpointsNotCovered = allEndpoints.Except(coveredEndpoints).ToArray(),

                ResponseRulesCovered = coveredResponseRules.ToArray(),
                ResponseRulesNotCovered = allResponseRules.Except(coveredResponseRules).ToArray()
            };
        }


        public abstract void WriteBeginTest(int index, NetmockeryTestCase testcase);
        public abstract void WriteResult(NetmockeryTestCaseResult result);
        public abstract void WriteResponse(string response);
        public abstract void WriteSummary(int errors);
        public abstract void WriteError(string s);
        public abstract void WriteLine(string s);
    }

    public class ConsoleTestRunner : TestRunner
    {
        public ConsoleTestRunner(EndpointCollection endpointCollection) : base(endpointCollection)
        {
        }

        public override void WriteBeginTest(int index, NetmockeryTestCase testcase)
        {
            Console.Write($"{index.ToString().PadLeft(3)} {testcase.Name.PadRight(60)}");
        }

        public override void WriteError(string s)
        {
            Console.Error.WriteLine($"ERROR: {s}");
        }

        public override void WriteResponse(string response)
        {
            if (Console.IsOutputRedirected)
            {
                Console.Write(response);
            }
            else
            {
                Console.WriteLine(response);
            }

        }

        public override void WriteResult(NetmockeryTestCaseResult result)
        {
            Console.WriteLine(result.ResultAsString);
        }

        public override void WriteSummary(int errors)
        {
            Console.WriteLine();
            Console.WriteLine($"Total: {Tests.Count()} Errors: {errors}");
        }

        public override void WriteLine(string s)
        {
            Console.WriteLine(s);
        }
    }

    public class CoverageInfo
    {
        public string[] EndpointsCovered;
        public string[] EndpointsNotCovered;

        public string[] ResponseRulesCovered;
        public string[] ResponseRulesNotCovered;
    }
}
