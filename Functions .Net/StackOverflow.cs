using System;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Web;
using System.Net.Http;
using System.Linq;
using System.Net;
using System.Threading;
using Polly;
namespace Company.Function
{
    
    
    public static class StackOverflow
    {
        private const int MAX_RETRIES = 10;

        private static Polly.Retry.AsyncRetryPolicy GetAsyncRetryPolicy(ILogger log){
            return Policy
                    .Handle<Exception>()
                    .WaitAndRetryAsync(MAX_RETRIES, 
                        retryAttempt =>
                          {
                              double waitTimeInSeconds = 16 + Math.Pow(2, retryAttempt);
                              log.LogWarning($"Call {retryAttempt}/{MAX_RETRIES} failed ! Trying again in {waitTimeInSeconds} seconds");
                              return TimeSpan.FromSeconds(waitTimeInSeconds);
                          },
                          (exception, timespan) => log.LogWarning($"Encountered exception : {exception.Message}")
                      );
        }
        [FunctionName("StackOverflowWrapper")]
        public static async Task<IActionResult> StackOverflowWrapper(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "StackOverflowWrapper")] HttpRequest req
            , ILogger log)
        {

            try
            {
                
            
                string urlquery = req.Query["urlquery"];
                log.LogInformation(urlquery);
                
                StackOverflowDTO so = await GetAsyncRetryPolicy(log).ExecuteAsync(() => GetStackOverflowData(urlquery) );
                //response = await client.GetAsync(urlquery);
                

                
                if(so.has_more)
                {
                    Uri uri = new Uri(urlquery);
                    var queryParts = HttpUtility.ParseQueryString(urlquery);
                    queryParts["page"] = (int.Parse(queryParts["page"])+1).ToString();
                    so.nextLink =  queryParts.ToString();
                }
                return new OkObjectResult(so);
            }catch(Exception e)
            {
                log.LogInformation(e.Message);
                return new BadRequestObjectResult(e.Message);
            }
        }
        public static async Task<StackOverflowDTO> GetStackOverflowData(string urlquery)
        {
                HttpClientHandler handler = new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.GZip
                };
                HttpClient client = new HttpClient(handler);

                var response = await client.GetAsync(urlquery);

                if(!response.IsSuccessStatusCode){
                    throw new Exception(await response.Content.ReadAsStringAsync());
                }
                
                return await response.Content.ReadAsAsync<StackOverflowDTO>();

                
            
        }
        public class StackOverflowDTO
        {
            public string nextLink {get;set;}
            public int backoff {get;set;}
            public bool has_more {get;set;}
            public int quota_max {get;set;}
            public int quota_remaining {get;set;}
            public object items {get;set;}
        }

    }
}