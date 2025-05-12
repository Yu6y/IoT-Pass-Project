using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using System.Text;

namespace Device
{
    public class VirtualDevice
    {
        private readonly DeviceClient client;

        public VirtualDevice(DeviceClient deviceClient)
        {
            this.client = deviceClient;
        }

        #region Sending Messages D2C

        public async Task SendMessages(DeviceData deviceData)
        {
            var rnd = new Random();
            Console.WriteLine($"Device sending data to IoT hub.. \n");

            var dataString = JsonConvert.SerializeObject(deviceData);
            Message eventMessage = new Message(Encoding.UTF8.GetBytes(dataString));
            eventMessage.ContentType = "application/json";
            eventMessage.ContentEncoding = "utf-8";
            eventMessage.Properties.Add("temperatureAlert", (deviceData.Temperature > 30 ? "true" : "false"));

            Console.WriteLine($"\t {DateTime.Now.ToLocalTime()} > Sending message!");

            await client.SendEventAsync(eventMessage);
        }
        #endregion

        #region Receiving Messages C2D

        private async Task OnC2DMessageReceivedAsync(Message receivedMessage, object _)
        {
            Console.WriteLine($"\t {DateTime.Now}> C2D message callback - message received with Id = {receivedMessage.MessageId}");
            PrintMessage(receivedMessage);

            await client.CompleteAsync(receivedMessage);
            Console.WriteLine($"\t {DateTime.Now}> Completed C2D message received with Id = {receivedMessage.MessageId}");
        }

        private void PrintMessage(Message receivedMessage)
        {
            string messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());
            Console.WriteLine($"\t\tReceived message: {messageData}");

            int propCount = 0;
            foreach (var prop in receivedMessage.Properties)
            {
                Console.WriteLine($"\t\t Property [{propCount++}] > Key = {prop.Key} Value = {prop.Value}");
            }
        }
        #endregion

        #region direct methods

        private async Task<MethodResponse> SendMessageHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"\t METHOD EXECUTED: {methodRequest.Name}");
            var payload = JsonConvert.DeserializeAnonymousType(methodRequest.DataAsJson, new DeviceData());
            await SendMessages(payload);
            return new MethodResponse(0);
        }

        private async Task<MethodResponse> DefaultServiceHandler(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"\t DEFAULT METHOD EXECUTED: {methodRequest.Name}");
            await Task.Delay(1000);
            return new MethodResponse(0);
        }
        #endregion

        #region Device Twin

        public async Task UpdateTwinAsync()
        {
            var twin = await client.GetTwinAsync();
            Console.WriteLine($"\n Initial twin value received: \n {JsonConvert.SerializeObject(twin, Formatting.Indented)}");
            Console.WriteLine();

            var reportedProperties = new TwinCollection();
            reportedProperties["DateTimeLastAppLaunch"] = DateTime.Now;
            await client.UpdateReportedPropertiesAsync(reportedProperties);
        }

        private async Task OnDesiredPropertyChanged(TwinCollection desiredProperties, object userContext)
        {
            Console.WriteLine($"\t Desired property change: \n\t {JsonConvert.SerializeObject(desiredProperties)}");
            Console.WriteLine("\t Sending current time as reported property");

            TwinCollection reportedProperties = new TwinCollection();
            reportedProperties["DateTimeLastDesiredPropertyChangeReceived"] = DateTime.Now;
            await client.UpdateReportedPropertiesAsync(reportedProperties);
        }
        #endregion

        public async Task InitializeHandlers()
        {
            await client.SetReceiveMessageHandlerAsync(OnC2DMessageReceivedAsync, client);

            await client.SetMethodHandlerAsync("SendMessages", SendMessageHandler, client);
            await client.SetMethodDefaultHandlerAsync(DefaultServiceHandler, client);
            await client.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChanged, client);
        }
    }
}
