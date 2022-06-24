using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Google.Ads.GoogleAds;
using Google.Ads.GoogleAds.Config;
using Google.Ads.GoogleAds.Lib;
using Google.Ads.GoogleAds.Util;
using Google.Ads.GoogleAds.V11.Errors;
using Google.Ads.GoogleAds.V11.Resources;
using Google.Ads.GoogleAds.V11.Services;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;

using Newtonsoft.Json;

using static Google.Ads.GoogleAds.V11.Enums.KeywordPlanForecastIntervalEnum.Types;

namespace HystericalMetrics
{
    internal class Program
    {
        private const string GOOGLE_ADS_API_SCOPE = "https://www.googleapis.com/auth/adwords";

        static void Main(string[] args)
        {
            var credCFG = (from file in Directory.GetFiles(@"C:\users\bugma\Credentials", "*.cfg") where file.Contains("trev") select file).FirstOrDefault();
            var credJSN = Path.ChangeExtension(credCFG, ".json");
            var devToken = (from line in File.ReadAllLines(credCFG) where line.StartsWith("developer.token") select line.Substring(16)).FirstOrDefault();
            var (adsClient, credential) = AuthoriseFromCFG(credCFG, "7212153394");
            CustomerServiceClient customerService = adsClient.GetService(Services.V11.CustomerService);
            foreach (var (clientid, text) in from long clientid in new long[]
            {
                1193902583,
                1347140419,
                1483715805,
                1483933215,
                1547657729,
                1729691812,
                1879809528,
                2104681232,
                2104997537,
                2267237197,
                2285124947,
                2394798900,
                2409825970,
                2435658713,
                2526688461,
                2562893933,
                2693505606,
                2818820764,
                2912568997,
                3199932296,
                3265653791,
                3418696531,
                3456230458,
                3478660029,
                3566786022,
                3646530754,
                3751361874,
                3814355224,
                3859046380,
                3880697725,
                3998547178,
                4017281433,
                4119300849,
                4166463837,
                4207533038,
                4244166419,
                4319869674,
                4348783421,
                4368168435,
                4461450409,
                4495408290,
                4684363102,
                4735353794,
                4805196135,
                4977158941,
                4982070868,
                5052971954,
                5079776886,
                5137629523,
                5353188421,
                5387411325,
                5407586039,
                5513257951,
                5559201319,
                5563399281,
                5576363583,
                5719196250,
                5836591055,
                5845871897,
                5982823872,
                6057354027,
                6100192546,
                6124804751,
                6206050729,
                6239854509,
                6253030355,
                6430638158,
                6478241056,
                6616417581,
                6750450433,
                6970639461,
                7127660622,
                7258074003,
                7265724868,
                7273576109,
                7381763716,
                7668259454,
                7688474169,
                7798813329,
                8197690984,
                8199738026,
                8200942313,
                8322972021,
                8361003066,
                8386741669,
                8528835613,
                8605857245,
                8865852054,
                8988629925,
                9020657546,
                9350110303,
                9589031868,
                9632511441,
                9685755031,
                9884395947,
                9968631413
            }
                                             let text = Path.Combine(Path.GetTempPath(), $"GoogleTrace_{DateTime.UtcNow:yyyy'-'MM'-'dd'-'HH'-'mm'-'ss'-'ffff}_{clientid}.log")
                                             select (clientid, text))
            {
                TraceUtilities.Configure(TraceUtilities.DETAILED_REQUEST_LOGS_SOURCE, text, SourceLevels.All);
                var (customer, exception) = GetAccountInformation(adsClient, clientid);
                if (customer != null)
                {
                    Console.WriteLine($"{clientid} {customer.DescriptiveName}");
                    var plan = CreateKeywordPlan(adsClient, clientid);
                    Console.WriteLine(plan);
                    var (metrics, exc) = GenerateHistoricalMetrics(adsClient, clientid, plan);
                    if (exc == null)
                        Console.WriteLine(metrics);
                }
            }
        }

        private static (GenerateHistoricalMetricsResponse response, GoogleAdsException exception) GenerateHistoricalMetrics(GoogleAdsClient client, long customerId, string plan)
        {
            KeywordPlanServiceClient kpServiceClient = client.GetService(Services.V11.KeywordPlanService);

            try
            {
                var response = kpServiceClient.GenerateHistoricalMetrics(plan);
                return (response, null);
            }
            catch (GoogleAdsException e)
            {
                return (null, e);
            }
        }

        private static (GoogleAdsClient adsClient, UserCredential credential) AuthoriseFromCFG(string cfgFile, string loginCustomerId, string scopes = GOOGLE_ADS_API_SCOPE, bool debug = false)
        {
            if (debug) Debugger.Launch();

            var cfgDict = new Dictionary<string, string>();
            foreach (var keyValue in from keyValue in
                                         from line in File.ReadAllLines(cfgFile) select line.Split('=')
                                     where !keyValue[0].StartsWith("#")
                                     select keyValue)
            {
                cfgDict[keyValue[0].Trim()] = keyValue[1].Trim();
            }

            dynamic jsonObj = JsonConvert.DeserializeObject(File.ReadAllText(Path.ChangeExtension(cfgFile, "json")));

            // Load the JSON secrets.
            ClientSecrets secrets = new ClientSecrets()
            {
                ClientId = (string)jsonObj.installed.client_id.Value,
                ClientSecret = (string)jsonObj.installed.client_secret,

            };

            // Authorize the user using desktop application flow.
            Task<UserCredential> task = GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets,
                scopes.Split(','),
                "user",
                CancellationToken.None,
                new FileDataStore("AdsAuth-" + Path.GetFileNameWithoutExtension(cfgFile), false)
            );
            UserCredential credential = task.Result;

            // Store this token for future use.

            // To make a call, set the refreshtoken to the config, and
            // create the GoogleAdsClient.
            GoogleAdsClient client = new GoogleAdsClient(new GoogleAdsConfig
            {
                OAuth2RefreshToken = credential.Token.RefreshToken,
                DeveloperToken = cfgDict["developer.token"],
                LoginCustomerId = loginCustomerId,
                OAuth2ClientId = (string)jsonObj.installed.client_id.Value,
                OAuth2ClientSecret = (string)jsonObj.installed.client_secret
            });
            // var cfgdata = client.Config;
            // Now use the client to create services and make API calls.
            // ...
            return (client, credential);
        }

        private static string CreateKeywordPlan(GoogleAdsClient client, long customerId)
        {
            // Get the KeywordPlanService.
            KeywordPlanServiceClient serviceClient = client.GetService(
                Services.V11.KeywordPlanService);

            // Create a keyword plan for next quarter forecast.
            KeywordPlan keywordPlan = new KeywordPlan()
            {
                Name = $"Keyword plan {System.Guid.NewGuid()}",
                ForecastPeriod = new KeywordPlanForecastPeriod()
                {
                    DateInterval = KeywordPlanForecastInterval.NextQuarter
                }
            };

            KeywordPlanOperation operation = new KeywordPlanOperation()
            {
                Create = keywordPlan
            };

            // Add the keyword plan.
            MutateKeywordPlansResponse response = serviceClient.MutateKeywordPlans(
                customerId.ToString(), new KeywordPlanOperation[] { operation });

            // Display the results.
            String planResource = response.Results[0].ResourceName;
            return planResource;
        }

        private static (Customer customer, GoogleAdsException exception) GetAccountInformation(GoogleAdsClient client, long customerId)
        {
            // Get the GoogleAdsService.
            GoogleAdsServiceClient googleAdsService = client.GetService(
                Services.V11.GoogleAdsService);

            // Construct a query to retrieve the customer.
            // Add a limit of 1 row to clarify that selecting from the customer resource
            // will always return only one row, which will be for the customer
            // ID specified in the request.
            string query = "SELECT customer.id, customer.descriptive_name, " +
                "customer.currency_code, customer.time_zone, customer.tracking_url_template, " +
                "customer.auto_tagging_enabled, customer.status FROM customer LIMIT 1";

            // Executes the query and gets the Customer object from the single row of the response.
            SearchGoogleAdsRequest request = new SearchGoogleAdsRequest()
            {
                CustomerId = customerId.ToString(),
                Query = query
            };

            try
            {
                // Issue the search request.
                Customer customer = googleAdsService.Search(request).First().Customer;

                // Print account information.
                return (customer, null);
            }
            catch (GoogleAdsException e)
            {
                return (null, e);
            }
        }
    }
}
