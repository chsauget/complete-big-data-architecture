using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives;
using System.Linq;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;
using Azure.Storage;
using Microsoft.Azure.Services.AppAuthentication;
using Azure.Identity;
using System.Web;

namespace Company.Function
{

    public static class SevenZip
    {
        [FunctionName("SevenZip")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "SevenZip/{account}/{container}/{directory}/{filename}")] HttpRequest req,
            string account, string container, string directory, string filename, ILogger log)
        {
            
            //Retrieve File from storage
            var lakeClient = GetDataLakeServiceClient(HttpUtility.UrlDecode(account));
            var fileSystemClient = lakeClient.GetFileSystemClient(HttpUtility.UrlDecode(container));
            DataLakeDirectoryClient directoryClient = fileSystemClient.GetDirectoryClient(HttpUtility.UrlDecode(directory));

            var DownloadFile = directoryClient.GetFileClient(HttpUtility.UrlDecode(filename));
            var ReadStream = await DownloadFile.OpenReadAsync();
            var response = req.HttpContext.Response;
            response.StatusCode = 200;
            response.ContentType = "application/json-data-stream";
            using (var archive = SevenZipArchive.Open(ReadStream, null))
            {
                foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                {
                    
                    foreach (IArchiveEntry e in archive.Entries)
                    {
                        
                        e.WriteTo(response.Body);                  
                    }

                }
            }
            return new EmptyResult();
        }
        public static DataLakeServiceClient GetDataLakeServiceClient(string accountName)
        {
            string dfsUri = "https://" + accountName + ".dfs.core.windows.net";
            var credential = new DefaultAzureCredential();
            return new DataLakeServiceClient (new Uri(dfsUri), credential);
        }

        
    }
}
