using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using System.Net;

namespace InvestFunctionApp.EndToEndTests
{
    public class AddToPortfolioFunctionShould
    {
        private string CreateInvestorFunctionKey;
        private string GetInvestorFunctionKey;
        private string PortfolioFunctionKey;
        private readonly ITestOutputHelper Output;

        private const string _baseTargetUri = "https://t-investfunc-test.azurewebsites.net";

        public AddToPortfolioFunctionShould(ITestOutputHelper output)
        {
            Output = output;
#if DEBUG
            SetTestEnvironmentVariables();
            output.WriteLine("It's DEBUG MODE");
#endif
            output.WriteLine("It's Release MODE");
            ReadTestEnvironmentVariables();            
        }

        private void SetTestEnvironmentVariables()
        {
            if (!File.Exists("Properties\\launchSettings.json")) return;

            // set [Copy to Output Dic] option of 'launchSettings.json' to 'Copy always'
            using (var file = File.OpenText("Properties\\launchSettings.json"))
            {
                var reader = new JsonTextReader(file);
                var jObject = JObject.Load(reader);
                
                var variables = jObject
                    .GetValue("profiles")
                //select a proper profile here
                .SelectMany(profiles => profiles.Children())
                .SelectMany(profile => profile.Children<JProperty>())
                .Where(prop => prop.Name == "environmentVariables")
                .SelectMany(prop => prop.Value.Children<JProperty>())
                .ToList();

                foreach (var variable in variables)
                {
                    Environment.SetEnvironmentVariable(variable.Name, variable.Value.ToString());
                }
            }
        }

        [Fact]
        public async Task BuyStocks()
        {
            const int initialValueOfStocks = 100;
            const int amountToInvest = 42;

            var startingInvestorDetails = new Investor
            {
                PartitionKey = "IndividualInvestor",
                RowKey = Guid.NewGuid().ToString(),
                CurrentValueOfStocks = initialValueOfStocks,
                CurrentValueOfBonds = 100,
                TargetPercentageAllocationToStocks = 75,
                TargetPercentageAllocationToBonds = 25
            };
            
            await CreateTestInvestorInTableStorage(startingInvestorDetails);
            await InvokeAddToPortfolioFunction(startingInvestorDetails.RowKey, amountToInvest);

            // Wait for a while
            await Task.Delay(TimeSpan.FromSeconds(10));

            var resultingInvestor = await GetInvestor(startingInvestorDetails.RowKey);

            Assert.Equal(initialValueOfStocks + amountToInvest, resultingInvestor.CurrentValueOfStocks);
        }

        private async Task CreateTestInvestorInTableStorage(Investor investor)
        {
            HttpStatusCode expectedCode = HttpStatusCode.OK;

            HttpClient client = new HttpClient();            
            HttpResponseMessage response = await client.PostAsJsonAsync($"{_baseTargetUri}/api/testing/createinvestor?code={CreateInvestorFunctionKey}", investor);

            response.EnsureSuccessStatusCode();
            Assert.Equal(expectedCode, response.StatusCode);
        }


        private async Task InvokeAddToPortfolioFunction(string investorId, int amount)
        {
            var url = $"{_baseTargetUri}/api/portfolio/{investorId}?code={PortfolioFunctionKey}";

            var deposit = new Deposit { Amount = amount };

            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.PostAsJsonAsync(url, deposit);
            response.EnsureSuccessStatusCode();
        }

        private async Task<Investor> GetInvestor(string investorId)
        {
            HttpClient client = new HttpClient();

            var response = await client.GetAsync($"{_baseTargetUri}/api/testing/getinvestor/{investorId}?code={GetInvestorFunctionKey}");
            response.EnsureSuccessStatusCode();

            return JsonConvert.DeserializeObject<Investor>(await response.Content.ReadAsStringAsync());
        }

        private void ReadTestEnvironmentVariables()
        {
            CreateInvestorFunctionKey = ReadEnvironmentVariable("CreateInvestorFunctionKey");
            GetInvestorFunctionKey = ReadEnvironmentVariable("GetInvestorInvestorFunctionKey");
            PortfolioFunctionKey = ReadEnvironmentVariable("PortfolioFunctionKey");
        }

        private string ReadEnvironmentVariable(string variableName)
        {
            var value = Environment.GetEnvironmentVariable(variableName);
            Output.WriteLine($"'{variableName}'='{value ?? "NOT SET"}'");
            return value;
        }
    }
}
