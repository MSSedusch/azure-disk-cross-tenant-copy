using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;
using Microsoft.Rest.Azure.Authentication;
using Newtonsoft.Json;

namespace azure_disk_cross_tenant_copy
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting...");
            
            var sourceToken = GetToken(
                Settings.sourceTenantID,
                Settings.sourceClientID,
                Settings.sourceClientSecret);

            var targetToken = GetToken(
                Settings.targetTenantID,
                Settings.targetClientID,
                Settings.targetClientSecret);


            string getRequestUri = "https://management.azure.com/subscriptions/88afd38d-1834-4d26-97f1-b26480709b11/resourceGroups/backup?api-version=2018-06-01";
            var getRequest = WebRequest.CreateHttp(getRequestUri);
            getRequest.Headers["Authorization"] = $"Bearer {sourceToken.access_token}";
            try
            {
                using (var reader = new StreamReader(getRequest.GetResponse().GetResponseStream()))
                {
                    string content = reader.ReadToEnd();
                    Console.WriteLine(content);
                }
            }
            catch (WebException wex)
            {
                if (wex.Response != null)
                {
                    using (var reader = new StreamReader(wex.Response.GetResponseStream()))
                    {
                        string error = reader.ReadToEnd();
                        Console.WriteLine(error);
                    }
                }
            }


            string putRequestUri = $"https://management.azure.com/{Settings.targetDiskId}?api-version=2018-06-01";
            var request = WebRequest.CreateHttp(putRequestUri);
            request.Method = "PUT";
            request.ContentType = "application/json";
            request.Headers["Authorization"] = $"Bearer {targetToken.access_token}";
            // request.Headers["Authorization"] = $"Bearer {sourceToken}";
            request.Headers["x-ms-authorization-auxiliary"] = $"Bearer {sourceToken.access_token}";

            using (var writer = new StreamWriter(request.GetRequestStream()))
            {
                writer.Write(@"
                    {
                        ""name"": """ + Settings.targetDiskName + @""",
                        ""location"": ""westeurope"",
                        ""properties"": {
                            ""creationData"": {
                                ""createOption"": ""Copy"",
                                ""sourceResourceId"": """ + Settings.sourceDiskId + @"""
                            }
                        }
                    }
                ");
                writer.Flush();
            }

            try
            {
                using (var reader = new StreamReader(request.GetResponse().GetResponseStream()))
                {
                        string result = reader.ReadToEnd();
                        Console.WriteLine(result);
                }
            }
            catch (WebException wex)
            {
                if (wex.Response != null)
                {
                    using (var reader = new StreamReader(wex.Response.GetResponseStream()))
                    {
                        string error = reader.ReadToEnd();
                        Console.WriteLine(error);
                    }
                }
            }


            Console.WriteLine("done");
            Console.ReadLine();
        }

        private static TokenResult GetToken(string tenantID, string clientID, string clientSecret)
        {
            string resource = "https://management.azure.com/";

            string loginURI = $"https://login.microsoftonline.com/{tenantID}/oauth2/token?api-version=1.0";
            var request = WebRequest.CreateHttp(loginURI);
            request.ContentType = "application/x-www-form-urlencoded";
            request.Method = "POST";
            string body = $"&grant_type=client_credentials&resource={HttpUtility.UrlEncode(resource)}&client_id={clientID}&client_secret={HttpUtility.UrlEncode(clientSecret)}";

            using (var writer = new StreamWriter(request.GetRequestStream()))
            {
                writer.Write(body);
                writer.Flush();
            }

            var content = String.Empty;
            var response = request.GetResponse();
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                content = reader.ReadToEnd();
            }

            return JsonConvert.DeserializeObject<TokenResult>(content);
        }
    }
}
