using System;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using nClam;

namespace techffie.blobscan
{
    public static class BlobAVScan
    {
        [FunctionName("BlobAVScan")]
        public static void Run([BlobTrigger("upload/{name}", Connection = "stgblobscan_STORAGE")]Stream myBlob, string name, ILogger log)
        {
            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");
            var clam = new ClamClient("clamavscan.southeastasia.azurecontainer.io", 3310);
            
            // Scanning for viruses...
            var scanResult = clam.SendAndScanFileAsync(myBlob).Result;

            switch (scanResult.Result)
            {
                case ClamScanResults.Clean:
                    log.LogInformation("The file is clean!");
                    break;
                case ClamScanResults.VirusDetected:
                    log.LogInformation("Virus Found!");
                    log.LogInformation("Virus name: {0}", scanResult.InfectedFiles.ToString());
                    break;
                case ClamScanResults.Error:
                    log.LogInformation("Error scanning file: {0}", scanResult.RawResult);
                    break;
            }
        }
    }
}
