using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Newtonsoft.Json;

namespace SendEmail
{
    public class SendEmailProxy
    {
        private readonly ILogger<SendEmailProxy> _logger;
        private readonly HttpClient _httpClient;

        public SendEmailProxy(ILogger<SendEmailProxy> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
        }

        [Function("SendEmailProxy")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("SendEmailProxy function triggered.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            string deviceId = data?.DeviceId;
            string timestamp = data?.Timestamp;
            var errorList = data?.ErrorList;

            if (deviceId == null || errorList == null)
            {
                var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Missing DeviceId or ErrorList.");
                return badResponse;
            }

            var payload = new
            {
                DeviceId = deviceId,
                Timestamp = timestamp,
                ErrorList = errorList
            };

            string logicAppUrl = Environment.GetEnvironmentVariable("LOGIC_APP_URL");

            if (string.IsNullOrWhiteSpace(logicAppUrl))
            {
                _logger.LogError("LOGIC_APP_URL environment variable is not set.");
                var errorResp = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResp.WriteStringAsync("Missing Logic App URL.");
                return errorResp;
            }

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(logicAppUrl, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Forwarded error to Logic App.");
                var okResp = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await okResp.WriteStringAsync("Email notification triggered.");
                return okResp;
            }
            else
            {
                _logger.LogError($"Logic App call failed: {response.StatusCode}");
                var failResp = req.CreateResponse(System.Net.HttpStatusCode.BadGateway);
                await failResp.WriteStringAsync($"Logic App call failed: {response.StatusCode}");
                return failResp;
            }
        }
    }
}

