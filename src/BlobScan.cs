using System;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using nClam;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace tech.blobscan
{
    public class BlobScan
    {
        [FunctionName("BlobScan")]
        public void Run([BlobTrigger("uploadblob/{name}", Connection = "blobmonitorconnstring")]Stream myBlob, string name, ILogger log)
        {
            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");

            string strClamAVServerFQDN = GetEnvironmentVariable("clamavserverfqdn");

            string sourceBlob = GetEnvironmentVariable("upload_blob_name");
            string cleanBlob = GetEnvironmentVariable("clean_blob_name");
            string quarantineBlob = GetEnvironmentVariable("quaratine_blob_name");

            int clamavport = Convert.ToInt32(GetEnvironmentVariable("clamavport"));
            ClamClient clam = new ClamClient(strClamAVServerFQDN,  clamavport);
            try{

                var scanResult = clam.SendAndScanFileAsync(myBlob).Result;            

                switch (scanResult.Result)
                {
                    case ClamScanResults.Clean:
                        log.LogInformation("The file is clean!");
                        string status = MoveFileFromBlob(name, sourceBlob, cleanBlob, log);
                        log.LogInformation("Move File {0} - {1}", status, name);
                        break;
                    case ClamScanResults.VirusDetected:
                        log.LogInformation("Virus Found!");
                        log.LogInformation("Virus name: {0}", scanResult.InfectedFiles.Count > 0 ? scanResult.InfectedFiles[0].FileName.ToString() : string.Empty);
                        MoveFileFromBlob(name, sourceBlob, quarantineBlob, log);
                        break;
                    case ClamScanResults.Error:
                        log.LogInformation("Error scanning file: {0}", scanResult.RawResult);
                        break;
                }
            }
            catch(Exception ex)
            {
                log.LogError(ex.Message.ToString());
            }
        }

        public static string GetEnvironmentVariable(string name)
        {
            return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }

        public static string MoveFileFromBlob(string sourceFileName, string sourceBlobName, string targetBlobName, ILogger log)
        {
            string connString = GetEnvironmentVariable("blobmonitorconnstring");
            string status = "Fail";
            if (!String.IsNullOrEmpty(connString))
            {
                BlobServiceClient blobClient = new BlobServiceClient(connString);
                var sourceContainer = blobClient.GetBlobContainerClient(sourceBlobName);
                var targetContainer = blobClient.GetBlobContainerClient(targetBlobName);

                var sourceBlob = sourceContainer.GetBlobClient(sourceFileName);
                var targetBlob = targetContainer.GetBlobClient(sourceFileName);

                if (sourceBlob.Exists())
                {
                    try
                    {                        
                        Response<BlobProperties> propertiesReponse = sourceBlob.GetProperties();
                        BlobProperties properties = propertiesReponse.Value;
                        targetBlob.StartCopyFromUri(sourceBlob.Uri);

                        status = targetBlob.Exists() ? "Success" : "Fail";
                        log.LogInformation("Move File Result : {0}", status);
                        if (targetBlob.Exists())
                        {
                            var deleteStatus = sourceBlob.DeleteIfExists();
                            log.LogInformation("Delete File Result : {0}", deleteStatus);
                        }
                    }
                    catch(Exception ex)
                    {
                        log.LogError(ex.Message.ToString());
                    }
                }               

                
            }
            return status;
        }

    }
}
