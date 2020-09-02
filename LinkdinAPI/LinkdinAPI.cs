using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System.Net.Http;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Linq;

namespace LinkdinAPI
{

    public class FeatchToken : TableEntity
    {
        public string Token { get; set; }

        public string ExpiryTime { get; set; }


    }

    //Json Parsing
    public class Accountdsdsd
    {
        public string access_token { get; set; }
        public string expires_in { get; set; }
    }

    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    public class ProfilePicture
    {
        public string displayImage { get; set; }
    }

    public class Localized
    {
        public string en_US { get; set; }
    }

    public class PreferredLocale
    {
        public string country { get; set; }
        public string language { get; set; }
    }

    public class FirstName
    {
        public Localized localized { get; set; }
        public PreferredLocale preferredLocale { get; set; }
    }

    public class Localized2
    {
        public string en_US { get; set; }
    }

    public class PreferredLocale2
    {
        public string country { get; set; }
        public string language { get; set; }
    }

    public class LastName
    {
        public Localized2 localized { get; set; }
        public PreferredLocale2 preferredLocale { get; set; }
    }

    public class Root
    {
        public string localizedLastName { get; set; }
        public ProfilePicture profilePicture { get; set; }
        public FirstName firstName { get; set; }
        public LastName lastName { get; set; }

        public PreferredLocale location { get; set; }
        public string id { get; set; }
        public string localizedFirstName { get; set; }
    }
    public static class LinkdinAPI
    {
        [FunctionName("LinkdinAPI")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ExecutionContext context,
            ILogger log)
        {
            var result = string.Empty;
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];

            if (name == null)
            {
                return new BadRequestObjectResult("Please pass id as query string");
            }

            var config = new ConfigurationBuilder().SetBasePath(context.FunctionAppDirectory).AddJsonFile("local.settings.json", optional: true, reloadOnChange: false).AddEnvironmentVariables().Build();
            var connectionstring = config["AzureWebJobsStorage"];


            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionstring);

            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable table = tableClient.GetTableReference("TokenDetails");
            string Timestampvalue = string.Empty;

            TableQuery<FeatchToken> query = new TableQuery<FeatchToken>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, name));

            var details = table.ExecuteQuerySegmentedAsync(query, null);

            // Assign the result to a Item object.
            var tokenavailable = details.Result;

            if (tokenavailable.Count() > 0)
            {
                var tokenvalue = tokenavailable.Results[0].Token;

                Timestampvalue = tokenavailable.Results[0].Timestamp.ToString();

                var tokenvalidity = CheckToken(Timestampvalue);

                if (!tokenvalidity)
                {
                    return new BadRequestObjectResult("Token is no more valid,Please refesh your token");
                }
                else
                {
                   
                  result=  UploadJson(tokenvalue, connectionstring, name);
                }



            }
            else
            {

                return new NotFoundObjectResult("No token available for this id");
               


            }

            if(result== "Success") 

            return new OkObjectResult("Json file successfully uploaded");
            else

                return new ObjectResult("json File upload failed");

        }


        private static string UploadJson(string token,string connectionstring,string name)
        {
            try
            {
                string value = string.Empty;
                using (var client = new HttpClient())
                {
                    var url = "https://api.linkedin.com/v2/me";
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

                    var response = client.GetStringAsync(url);

                    value = response.Result;
                }
                Root root = JsonConvert.DeserializeObject<Root>(value);




                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionstring);

                CloudBlobClient blobclient = storageAccount.CreateCloudBlobClient();

                CloudBlobContainer container = blobclient.GetContainerReference("profilevalue");

                string localPath = Path.GetTempPath();

                string fileName = name + ".json";

                string localFilePath = Path.Combine(localPath, fileName);


                File.WriteAllText(localFilePath, value);


                CloudBlockBlob blobClient = container.GetBlockBlobReference(fileName);


                blobClient.Properties.ContentType = "application/json";
                blobClient.SetPropertiesAsync();

                // Open the file and upload its data
                using (FileStream uploadFileStream = File.OpenRead(localFilePath))
                {

                    blobClient.UploadFromStreamAsync(uploadFileStream);
                    uploadFileStream.Close();
                }
            }
            catch
            {
                return "Error";
            }

            return "Success";

        }

        private static bool CheckToken(string Timestampvalue)
        {
            DateTime expirydate = DateTime.Now.AddMonths(2);

            if (Convert.ToDateTime(Timestampvalue) < expirydate)


                return true;
            else
                return false;

        }
    }
}