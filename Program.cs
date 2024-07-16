using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Auto_StckUpd_FA_4._5
{
    class Program
    {
        private static readonly HttpClient client;
        private string connectionString = "Data Source=10.20.4.41;Connection Timeout=6000;Initial Catalog=Synapse_Prod;Persist Security Info=True;User ID=sa;Password=wcclg66Synapse; Integrated Security=False; TrustServerCertificate=True";

        static Program()
        {
            client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(50);
        }

        static async Task Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            FetchDataFromAPI().GetAwaiter().GetResult();
        }

        public static async Task FetchDataFromAPI()
        {
            Program program = new Program();
            await program.FetchLatestUpdateStocks();
        }

        public async Task FetchLatestUpdateStocks()
        {
            SqlConnection connection = null;

            try
            {
                List<StockData> payloads = new List<StockData>();

                connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // Execute stored procedure
                using (var command = new SqlCommand("NonWisdomPulse_Distprdmap_UpdStck_CurrentStocks", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var distErpId = reader["DistErpId"].ToString();
                        var productErpId = reader["ProductERPId"].ToString();
                        var quantity = Convert.ToInt32(reader["Quantity"]);

                        payloads.Add(new StockData
                        {
                            DistErpId = distErpId,
                            ProductERPId = productErpId,
                            Quantity = quantity,
                            Unit = "pcs" // Assuming 'pcs' is the default unit
                        });
                    }
                }

                await UpdateStock(payloads);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
            finally
            {
                if (connection != null && connection.State != ConnectionState.Closed)
                {
                    connection.Close();
                }
            }
        }

        public async Task UpdateStock(List<StockData> payloads)
        {
            try
            {
                int totalCount = payloads.Count;
                int successCount = 0;
                List<object> failedUpdates = new List<object>();

                var userName = "Wipro_CC_Integration";
                var authKey = "pYbcDOXNmTDuT^)3iiG9";
                var authToken = Encoding.ASCII.GetBytes($"{userName}:{authKey}");
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));

                foreach (var payload in payloads)
                {
                    var apiUrl = $"https://api.fieldassist.in/api/V3/Distributor/UpdateStock/{payload.DistErpId}";

                    var payloadObject = new[]
                    {
                        new
                        {
                            ProductERPId = payload.ProductERPId,
                            Quantity = payload.Quantity,
                            Unit = payload.Unit
                        }
                    };

                    var payloadJson = JsonConvert.SerializeObject(payloadObject);

                    var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(apiUrl, content);

                    if (response.IsSuccessStatusCode)
                    {
                        successCount++;
                    }
                    else
                    {
                        failedUpdates.Add(new
                        {
                            DistErpId = payload.DistErpId,
                            ProductErpId = payload.ProductERPId,
                            Quantity = payload.Quantity,
                            StatusCode = response.StatusCode
                        });
                    }
                }

                var result = new
                {
                    TotalCount = totalCount,
                    StockUpdated = successCount,
                    StockUpdateFailed = failedUpdates
                };

                Console.WriteLine(JsonConvert.SerializeObject(result));
            }
            catch (Exception ex)
            {
                // Handle exceptions
                Console.WriteLine($"An error occurred while updating stocks: {ex.Message}");
            }
        }

        #region Models
        public class StockData
        {
            public string DistErpId { get; set; }
            public string ProductERPId { get; set; }
            public int Quantity { get; set; }
            public string Unit { get; set; } = "pcs";
        }

        public class FailedUpdate
        {
            public string DistErpId { get; set; }
            public string ProductErpId { get; set; }
            public int Quantity { get; set; }
            public System.Net.HttpStatusCode StatusCode { get; set; }
        }
        #endregion 

    }
}

