using System;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using nClam;

namespace techffie.blobscan
{
    public static class BlobAVScan
    {
        [FunctionName("BlobAVScan")]
        public static void Run([BlobTrigger("upload/{name}", Connection = "stgblobscan_STORAGE")]Stream myBlob, string name, ILogger log)
        {
            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");
            var clam = new ClamClient(GetEnvironmentVariable("clamavserver"), Convert.ToInt32(GetEnvironmentVariable("clamavport")));
            
            // Scanning for viruses...
            var scanResult = clam.SendAndScanFileAsync(myBlob).Result;            

            switch (scanResult.Result)
            {
                case ClamScanResults.Clean:
                    log.LogInformation("The file is clean!");
                    MoveFileFromBlob(name);
                    log.LogInformation("Move File {0}", name);
                    break;
                case ClamScanResults.VirusDetected:
                    log.LogInformation("Virus Found!");
                    log.LogInformation("Virus name: {0}", scanResult.InfectedFiles.Count > 0 ? scanResult.InfectedFiles[0].FileName.ToString() : string.Empty);
                    break;
                case ClamScanResults.Error:
                    log.LogInformation("Error scanning file: {0}", scanResult.RawResult);
                    break;
            }
        }

        public static string GetEnvironmentVariable(string name)
        {
            return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }

        public static string MoveFileFromBlob(string sourceFileName)
        {
            CloudStorageAccount storageAccount = new CloudStorageAccount(new StorageCredentials(GetEnvironmentVariable("account_name"), GetEnvironmentVariable("key_value")), true);
            CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer sourceContainer = cloudBlobClient.GetContainerReference(GetEnvironmentVariable("upload_blob_name"));
            CloudBlobContainer targetContainer = cloudBlobClient.GetContainerReference(GetEnvironmentVariable("quarantine_blob_name"));
            
            CloudBlockBlob sourceBlob = sourceContainer.GetBlockBlobReference(sourceFileName);
            CloudBlockBlob targetBlob = targetContainer.GetBlockBlobReference(sourceFileName);
            return targetBlob.StartCopyAsync(sourceBlob).Result;
        }
    }
}
