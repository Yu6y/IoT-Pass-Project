using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Devices;
using Newtonsoft.Json;

namespace UpdateProductionRate
{
    public class UpdateProductionRateFunction
    {
        private readonly ILogger _logger;
        private static RegistryManager? registryManager = null;

        public UpdateProductionRateFunction(ILogger<UpdateProductionRateFunction> logger)
        {
            _logger = logger;
            var connectionString = Environment.GetEnvironmentVariable("IOTHUB_CONNECTION_STRING");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("IOTHUB_CONNECTION_STRING environment variable is not set.");
            }
            registryManager ??= RegistryManager.CreateFromConnectionString(connectionString);
        }

        [Function("UpdateProductionRateFunction")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("UpdateProductionRateFunction triggered.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            string deviceId = data?.DeviceId;
            string targetMachine = data?.TargetMachine;

            if (string.IsNullOrEmpty(deviceId))
            {
                var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Missing DeviceId.");
                return badResponse;
            }

            if (string.IsNullOrEmpty(targetMachine))
            {
                var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Missing TargetMachine.");
                return badResponse;
            }

            double productionKpis = 100.0;
            try
            {
                if (data?.productionKpis != null)
                {
                    productionKpis = (double)data.productionKpis;
                }
                else
                {
                    var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Missing productionKpis.");
                    return badResponse;
                }
            }
            catch (Exception)
            {
                var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid productionKpis format.");
                return badResponse;
            }

            if (productionKpis >= 90.0)
            {
                var noUpdateResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await noUpdateResponse.WriteStringAsync($"Production KPI ({productionKpis}) is above threshold. No update made.");
                return noUpdateResponse;
            }

            try
            {
                var twin = await registryManager!.GetTwinAsync(deviceId);
                var propertyPath = targetMachine.Replace(" ", "/"); 

                int currentRate = 100;
                if (twin.Properties.Desired.Contains(propertyPath) &&
                    twin.Properties.Desired[propertyPath]["ProductionRate"] != null)
                {
                    currentRate = (int)twin.Properties.Desired[propertyPath]["ProductionRate"];
                }

                int newRate = Math.Max(0, currentRate - 10);

                string patch = $@"
                {{
                  ""properties"": {{
                    ""desired"": {{
                      ""{propertyPath}"": {{
                        ""ProductionRate"": {newRate}
                      }}
                    }}
                  }}
                }}";

                await registryManager.UpdateTwinAsync(deviceId, patch, twin.ETag);

                var okResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await okResponse.WriteStringAsync($"ProductionRate for {targetMachine} updated to {newRate}%. KPI was {productionKpis}.");
                return okResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating ProductionRate: {ex.Message}");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Failed to update ProductionRate: {ex.Message}");
                return errorResponse;
            }
        }
    }
}
