using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Logging;
using Microsoft.AnalysisServices.AdomdClient;
using System;
using System.IO;

namespace Company.Function
{
    public static class SSASTesting
    {
        public class TestingBody
        {
            public string serverName {get;set;}
            public string  scope {get;set;}
            public string dbName {get;set;}

            public string query {get;set;}
            public string testId {get;set;}
            public int nbConcurrentQuery {get;set;}
           
        }
        [FunctionName("SSASTestingPerf")]
        public static async Task SSASTestingPerf(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            TestingBody body = context.GetInput<TestingBody>();
            
            await context.CallActivityAsync("SSASTestingPerf_Activity", (body,"Query1"));
            await context.CallActivityAsync("SSASTestingPerf_Activity", (body,"Query2"));
            await context.CallActivityAsync("SSASTestingPerf_Activity", (body,"Query3"));
           
        }
        [FunctionName("SSASTestingConcurrency")]
        public static async Task SSASTestingConcurrency(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            TestingBody body = context.GetInput<TestingBody>();
            
            List<Task> t = new List<Task>();
            for(var i = 1;i<=body.nbConcurrentQuery;i++)
            {
                var newBody = new TestingBody{
                                            dbName=body.dbName
                                            ,query=body.query.Replace("{id}",i.ToString())
                                            ,scope=body.scope
                                            ,serverName=body.serverName
                                            ,testId=body.testId
                                        };
                t.Add(context.CallActivityAsync("SSASTestingPerf_Activity", (newBody,$"Query{i}")));
            }
            await Task.WhenAll(t);
           
        }
        [FunctionName("SSASTestingPerf_Activity")]
        public static async Task<string> SSASTestingPerfActivity([ActivityTrigger] (TestingBody body,string testCase) param, ILogger log)
        {
            return await RunDaxQuery(param.body.testId,param.body.scope,param.testCase,param.body.query,param.body.serverName, param.body.dbName,log);
        }

        [FunctionName("SSASTestingPerf_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {

            var body = await req.Content.ReadAsAsync<TestingBody>();
            
            // Function input comes from the request content.

            string instanceId = await starter.StartNewAsync(body.scope, null,body);
            
            
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
        
        public static async Task<string> RunDaxQuery(string testId,string scope,string testCase,string query,string serverName, string dbName,ILogger log)
        {
            DateTime dateStart = DateTime.Now;
            using (var connection = new AdomdConnection(await GetConnectionString("northeurope"
                                    ,"01cad88a-b9c8-4c54-ad8d-c6920c250e4b"
                                    ,serverName
                                    ,dbName)))
            {
                connection.Open();
                AdomdCommand cmd = new AdomdCommand(query);
                cmd.Connection = connection;
                using (var reader = cmd.ExecuteReader()) {
                    while (reader.Read()) {
                        //log.LogInformation(reader[0].ToString());
                    }
                }
            }
            await WriteLogs(testId,serverName,scope,testCase,dateStart,DateTime.Now);
            return "ok";
            
        }
        private static async Task<string> GetConnectionString(string aasRegion,string tenantId, string aasServerName, string aasDatabaseName)
        {
            var azureServiceTokenProvider = new AzureServiceTokenProvider();
            var token = await azureServiceTokenProvider.GetAccessTokenAsync($"https://{aasRegion}.asazure.windows.net", tenantId);
            return $"Provider=MSOLAP;" +
                $"Data Source={aasServerName};" +
                $"Initial Catalog={aasDatabaseName};" +
                $"User ID=;" +
                $"Password={token};" +
                $"Persist Security Info=True;" +
                $"Impersonation Level=Impersonate";
        }
        public static async Task WriteLogs(string testId,string serverName,string scope,string message,DateTime dateStart,DateTime dateEnd)
        {
            using(StreamWriter file = new StreamWriter("c:/temp/log.txt",true))
            {
                await file.WriteLineAsync($"{testId},{serverName},{scope},{message},{dateStart},{dateEnd},{dateEnd.Subtract(dateStart).ToString()}");
                file.Close();
            }
        }
    }
}