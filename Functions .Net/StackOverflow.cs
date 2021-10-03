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
namespace Company.Function
{
    
    public static class StackOverflow
    {
        [FunctionName("StackOverflowWrapper")]
        public static async Task<IActionResult> StackOverflowWrapper(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "StackOverflowWrapper")] HttpRequest req
            , ILogger log)
        {
            HttpResponseMessage response = new HttpResponseMessage();
            try
            {
                
            
                string urlquery = req.Query["urlquery"];
                log.LogInformation(urlquery);
                HttpClientHandler handler = new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.GZip
                };
                HttpClient client = new HttpClient(handler);
                response = await client.GetAsync(urlquery);
                response.EnsureSuccessStatusCode();
                StackOverflowDTO so = new StackOverflowDTO();
                
                so = await response.Content.ReadAsAsync<StackOverflowDTO>();
                
                if(so.has_more)
                {
                    Uri uri = new Uri(urlquery);
                    var queryParts = HttpUtility.ParseQueryString(urlquery);
                    queryParts["page"] = (int.Parse(queryParts["page"])+1).ToString();
                    so.nextLink =  queryParts.ToString();
                }
                Thread.Sleep(5000);
                return new OkObjectResult(so);
            }catch(Exception e)
            {
                log.LogInformation(await response.Content.ReadAsStringAsync());
                return new BadRequestObjectResult(e.Message);
            }
        }
        public class StackOverflowDTO
        {
            public string nextLink {get;set;}
            public bool has_more {get;set;}
            public int quota_max {get;set;}
            public int quota_remaining {get;set;}
            public object items {get;set;}
        }

    }
}