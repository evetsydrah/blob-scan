using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using nClam;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;


namespace BlobScan.Function
{
    public class testingblobscan
    {
        private readonly ILogger<testingblobscan> _logger;

        public testingblobscan(ILogger<testingblobscan> logger)
        {
            _logger = logger;
        }

        [Function(nameof(testingblobscan))]
        public async Task Run([BlobTrigger("uploadblob/{name}", Connection = "blobmonitorconnstring")] Stream myBlob, string name)
        {
            //using var blobStreamReader = new StreamReader(myBlob);
            //var content = await blobStreamReader.ReadToEndAsync();
            //_logger.LogInformation($"C# Blob trigger function Processed blob\n Name: {name} \n Data: {content}");
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
                        _logger.LogInformation("The file is clean!");
                        string status = MoveFileFromBlob(name, sourceBlob, cleanBlob, _logger);
                        _logger.LogInformation("Move File {0} - {1}", status, name);
                        break;
                    case ClamScanResults.VirusDetected:
                        _logger.LogInformation("Virus Found!");
                        _logger.LogInformation("Virus name: {0}", scanResult.InfectedFiles.Count > 0 ? scanResult.InfectedFiles[0].FileName.ToString() : string.Empty);
                        MoveFileFromBlob(name, sourceBlob, quarantineBlob, _logger);
                        break;
                    case ClamScanResults.Error:
                        _logger.LogInformation("Error scanning file: {0}", scanResult.RawResult);
                        break;
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex.Message.ToString());
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
