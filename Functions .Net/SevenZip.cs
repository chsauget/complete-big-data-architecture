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

namespace Company.Function
{
    //https://icsharpcode.github.io/SharpZipLib/help/api/ICSharpCode.SharpZipLib.BZip2.BZip2.html
    //https://www.oreilly.com/library/view/windows-developer-power/0596527543/ch04s11.html
    public static class SevenZip
    {
        [FunctionName("SevenZip")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            
            //Retrieve File from storage
            var lakeClient = GetDataLakeServiceClient("synapselakeapp");
            var fileSystemClient = lakeClient.GetFileSystemClient("lake");
            DataLakeDirectoryClient directoryClient = fileSystemClient.GetDirectoryClient("StackOverflow/raw/");
            var file = directoryClient.GetFileClient("stackoverflow.com-PostHistory.7z");
            System.IO.Stream sr = (await file.ReadAsync()).Value.Content;
            
            using (var fileStream = File.Create(@"D:\GIT\complete-big-data-architecture\Functions .Net\test.xml"))
            {
                using (var archive = SevenZipArchive.Open(sr, null))
                {
                    foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                    {

                        foreach (IArchiveEntry e in archive.Entries)
                        {
                            e.WriteTo(fileStream);
                        }

                    }
                }
            }
                
            
            
            return new OkObjectResult("ok");
        }
        public static DataLakeServiceClient GetDataLakeServiceClient(string accountName)
        {
            string dfsUri = "https://" + accountName + ".dfs.core.windows.net";
            var credential = new DefaultAzureCredential();
            return new DataLakeServiceClient (new Uri(dfsUri), credential);
        }

        
    }
}
