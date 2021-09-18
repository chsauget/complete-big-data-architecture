using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives;
using System.Linq;
using Azure.Storage.Files.DataLake;
using System.Web;
using System.Xml;
using System.Data;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;
using Newtonsoft.Json;
namespace Company.Function
{

    public static class SevenZipV2
    {
        [FunctionName("SevenZipV2")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "SevenZipV2/{account}/{container}/{directory}/{filename}")] HttpRequest req,
            string account, string container, string directory, string filename, ILogger log)
        {
            
            //Retrieve File from storage
            var lakeClient = SevenZip.GetDataLakeServiceClient(HttpUtility.UrlDecode(account));
            var fileSystemClient = lakeClient.GetFileSystemClient(HttpUtility.UrlDecode(container));
            DataLakeDirectoryClient directoryClient = fileSystemClient.GetDirectoryClient(HttpUtility.UrlDecode(directory));

            var DownloadFile = directoryClient.GetFileClient(HttpUtility.UrlDecode(filename));
            var ReadStream = await DownloadFile.OpenReadAsync();

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.Async = true;
            
          
            var response = req.HttpContext.Response;
            response.StatusCode = 200;
            response.ContentType = "application/json-data-stream";
            await response.Body.WriteAsync(Encoding.UTF8.GetBytes("["));
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            using (var archive = SevenZipArchive.Open(ReadStream, null))
            {
                foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                {
                    IArchiveEntry e = archive.Entries.FirstOrDefault(); 

                        var un7ZipStream = e.OpenEntryStream();

                        using (XmlReader reader = XmlReader.Create(un7ZipStream, settings))
                        {
                            bool keepReading = reader.Read();
                            bool firstrow = true;
                            while(keepReading)
                            {
                                try
                                {
                                    if((reader.NodeType == XmlNodeType.Element) && (reader.Name == "row") && reader.HasAttributes)
                                    {

                                        dynamic exo = new ExpandoObject();   
                                        for (int attInd = 0; attInd < reader.AttributeCount; attInd++){
                                                
                                            reader.MoveToAttribute( attInd );
                                            ((IDictionary<String, Object>)exo).Add(reader.Name, reader.Value);
                                            }
                                        await response.Body.WriteAsync(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(exo)));
                                                            
                                    }
                                    if(reader.Read())
                                    {
                                        if((reader.NodeType == XmlNodeType.Element) && (reader.Name == "row") && reader.HasAttributes)
                                        {
                                            if(!firstrow)
                                            {
                                                await response.Body.WriteAsync(Encoding.UTF8.GetBytes(","));
                                            }
                                            else
                                            {
                                                firstrow=false;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        keepReading = false;
                                    }  
                                }catch(Exception ex)
                                {
                                    log.LogInformation(ex.Message);
                                }
                            }
                        }

                }
            }
            await response.Body.WriteAsync(Encoding.UTF8.GetBytes("]"));
            return new EmptyResult();
        }


        
    }
}
