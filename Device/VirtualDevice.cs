﻿using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Opc.UaFx.Client;
using System.Text;
using System.Text.Json;

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

        public async Task SendMessages(DeviceData deviceData, string deviceId)
        {
            Console.WriteLine(deviceId);
            var dataString = JsonConvert.SerializeObject(deviceData);
            var eventMessage = new Message(Encoding.UTF8.GetBytes(dataString))
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8"
            };            

            Console.WriteLine($"{DateTime.Now:HH:mm:ss} > Sending message to IoT Hub...");
            await client.SendEventAsync(eventMessage);
        }

        public async Task SendDeviceErrorMessage(int value, string deviceId)
        {
            Console.WriteLine($"An error occured on {deviceId}!");
            string flag = Convert.ToString(value, 2).PadLeft(4, '0');
            Dictionary<string, object> message = new Dictionary<string, object>();
            List<string> errors = new List<string>();

            if (flag[3] == '1')
                errors.Add("Emergency Stop");
            if (flag[2] == '1')
                errors.Add("Power Failure");
            if (flag[1] == '1')
                errors.Add("Sensor Failure");
            if (flag[0] == '1')
                errors.Add("Unknown");

            message.Add("DeviceId", deviceId);
            message.Add("Errors", errors);
            message.Add("Value", value);
            message.Add("Timestamp", DateTime.Now);

            var dataString = JsonConvert.SerializeObject(message);
            var eventMessage = new Message(Encoding.UTF8.GetBytes(dataString))
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8"
            };

            Console.WriteLine($"{DateTime.Now:HH:mm:ss} > Sending error message to IoT Hub...");
            await client.SendEventAsync(eventMessage);
        }

        public async Task UpdateTwinAsync(DeviceData deviceData, string deviceId)
        {
            var reportedProperties = new TwinCollection();
            reportedProperties[deviceId.Replace(' ', '/')] = new
            {
                DateTimeLastAppLaunch = DateTime.Now,
                deviceData.ProductionRate,
                deviceData.DeviceErrors
            };

            await client.UpdateReportedPropertiesAsync(reportedProperties);
            Console.WriteLine("Reported properties updated.");
        }

        private async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            var reportedProperties = new TwinCollection();
            var properties = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(desiredProperties.ToJson());

            foreach(var row in properties)
            {
                if (row.Key.StartsWith("Device"))
                {
                    string deviceId = row.Key;
                    JsonElement deviceData = row.Value;
                    
                    if (deviceData.TryGetProperty("ProductionRate", out JsonElement productionRate))
                    {
                        int desiredRate = productionRate.GetInt32();

                        Console.WriteLine($"Setting ProductionRate to {desiredRate}");
                        opcClient.WriteNode($"ns=2;s={deviceId.Replace('/', ' ')}/ProductionRate", desiredRate);

                        reportedProperties[deviceId] = new
                        {
                            DateTimeLastDesiredPropertyChangeReceived = DateTime.Now
                        };

                        await client.UpdateReportedPropertiesAsync(reportedProperties);
                    }
                }
            }
        }

        private async Task<MethodResponse> EmergencyStopHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine(">>> Emergency Stop received from cloud!");
            var payload = JsonConvert.DeserializeAnonymousType(methodRequest.DataAsJson, new { machine = default(string) });
            try
            {
                opcClient.CallMethod($"ns=2;s={payload.machine}", $"ns=2;s={payload.machine}/EmergencyStop");
                return new MethodResponse(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] EmergencyStopHandler: {ex.Message}");
                return new MethodResponse(1);
            }
        }


        private async Task<MethodResponse> ResetErrorStatusHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine(">>> Reset Error Status received from cloud!");
            var payload = JsonConvert.DeserializeAnonymousType(methodRequest.DataAsJson, new { machine = default(string) });
            try
            {
                opcClient.CallMethod($"ns=2;s={payload.machine}", $"ns=2;s={payload.machine}/ResetErrorStatus");
                return new MethodResponse(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ResetErrorStatusHandler: {ex.Message}");
                return new MethodResponse(1);
            }
        }


        private async Task<MethodResponse> DefaultServiceHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"Default method executed: {methodRequest.Name}");
            return new MethodResponse(0);
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
