using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Opc.UaFx;
using Opc.UaFx.Client;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Device
{
    public class VirtualDevice
    {
        private readonly DeviceClient client;
        private readonly OpcClient opcClient;

        public VirtualDevice(DeviceClient deviceClient, OpcClient opcClient)
        {
            this.client = deviceClient;
            this.opcClient = opcClient;
        }

        public async Task SendMessages(DeviceData deviceData)
        {
            var dataString = JsonConvert.SerializeObject(deviceData);
            var eventMessage = new Message(Encoding.UTF8.GetBytes(dataString))
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8"
            };

            eventMessage.Properties.Add("temperatureAlert", deviceData.Temperature > 30 ? "true" : "false");

            Console.WriteLine($"{DateTime.Now:HH:mm:ss} > Sending message to IoT Hub...");
            await client.SendEventAsync(eventMessage);
        }

        public async Task UpdateTwinAsync(DeviceData deviceData)
        {
            var reportedProperties = new TwinCollection
            {
                ["DateTimeLastAppLaunch"] = DateTime.Now,
                ["ProductionRate"] = deviceData.ProductionRate,
                ["DeviceErrors"] = deviceData.DeviceErrors
            };

            await client.UpdateReportedPropertiesAsync(reportedProperties);
            Console.WriteLine("Reported properties updated.");
        }

        private async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            Console.WriteLine($"Desired property change: {JsonConvert.SerializeObject(desiredProperties)}");

            var reportedProperties = new TwinCollection
            {
                ["DateTimeLastDesiredPropertyChangeReceived"] = DateTime.Now
            };

            if (desiredProperties.Contains("ProductionRate"))
            {
                int desiredRate = desiredProperties["ProductionRate"];
                Console.WriteLine($"Setting ProductionRate to {desiredRate}");
                opcClient.WriteNode("ns=2;s=Device 1/ProductionRate", desiredRate);
                reportedProperties["ProductionRateApplied"] = desiredRate;
            }

            await client.UpdateReportedPropertiesAsync(reportedProperties);
        }

        private async Task<MethodResponse> EmergencyStopHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine(">>> Emergency Stop received from cloud!");

            try
            {
                opcClient.WriteNode("ns=2;s=Device 1/EmergencyStop", true); 
                return new MethodResponse(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] EmergencyStopHandler: {ex.Message}");
                return new MethodResponse(500);
            }
        }


        private async Task<MethodResponse> ResetErrorStatusHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine(">>> Reset Error Status received from cloud!");

            try
            {
                opcClient.WriteNode("ns=2;s=Device 1/ResetErrorStatus", true);
                return new MethodResponse(200);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ResetErrorStatusHandler: {ex.Message}");
                return new MethodResponse(500);
            }
        }


        private async Task<MethodResponse> DefaultServiceHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"Default method executed: {methodRequest.Name}");
            return new MethodResponse(404);
        }

        private async Task OnC2DMessageReceivedAsync(Message receivedMessage, object _)
        {
            string messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());
            Console.WriteLine($"Received C2D message: {messageData}");
            await client.CompleteAsync(receivedMessage);
        }

        public async Task InitializeHandlers()
        {
            await client.SetReceiveMessageHandlerAsync(OnC2DMessageReceivedAsync, client);
            await client.SetMethodHandlerAsync("EmergencyStop", EmergencyStopHandler, client);
            await client.SetMethodHandlerAsync("ResetErrorStatus", ResetErrorStatusHandler, client);
            await client.SetMethodDefaultHandlerAsync(DefaultServiceHandler, client);
            await client.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, client);
        }
    }
}
