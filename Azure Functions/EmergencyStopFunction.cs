using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Devices;

namespace EmergencyStop
{
    public class EmergencyStopFunction
    {
        private readonly ILogger _logger;
        private static ServiceClient? serviceClient = null;

        public EmergencyStopFunction(ILogger<EmergencyStopFunction> logger)
        {
            _logger = logger;
            var connectionString = Environment.GetEnvironmentVariable("IOTHUB_CONNECTION_STRING");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("IOTHUB_CONNECTION_STRING environment variable is not set.");
            }
            serviceClient ??= ServiceClient.CreateFromConnectionString(connectionString);
        }

        [Function("EmergencyStopFunction")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("EmergencyStopFunction triggered.");

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

            try
            {
                var payload = new
                {
                    machine = targetMachine
                };

                var methodInvocation = new CloudToDeviceMethod("EmergencyStop")
                {
                    ResponseTimeout = TimeSpan.FromSeconds(30)
                };
                methodInvocation.SetPayloadJson(JsonConvert.SerializeObject(payload));

                _logger.LogInformation($"Sending EmergencyStop direct method to device '{deviceId}' with payload: {JsonConvert.SerializeObject(payload)}");

                var response = await serviceClient.InvokeDeviceMethodAsync(deviceId, methodInvocation);

                var okResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await okResponse.WriteStringAsync($"Emergency Stop triggered on device '{deviceId}' for target machine '{targetMachine}'.");
                return okResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending EmergencyStop to {deviceId}: {ex.Message}");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Failed to trigger EmergencyStop: {ex.Message}");
                return errorResponse;
            }
        }
    }
}
