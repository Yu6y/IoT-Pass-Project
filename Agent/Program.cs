using Microsoft.Azure.Devices.Client;
using Device;
using Opc.UaFx.Client;
using Opc.UaFx;
using System.Text.Json;
using Org.BouncyCastle.Security;

namespace Agent
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            string prop = File.ReadAllText("application-properties.json");
            Configuration connectionString = JsonSerializer.Deserialize<Configuration>(prop);

            if (string.IsNullOrEmpty(connectionString.DeviceConnectionString))
                return;

            using var opcClient = new OpcClient("opc.tcp://localhost:4840/");
            using var deviceClient = DeviceClient.CreateFromConnectionString(connectionString.DeviceConnectionString);
            
            try
            {
                opcClient.Connect();
                await deviceClient.OpenAsync();

                var device = new VirtualDevice(deviceClient, opcClient);
                Dictionary<string, int> prevErrors = new Dictionary<string, int>();
                await device.InitializeHandlers();

                Console.WriteLine("Agent started. Press Ctrl+C to stop.\n");

                while (true)
                {
                    OpcNodeInfo objectsNode = opcClient.BrowseNode(OpcObjectTypes.ObjectsFolder);

                    foreach (var childNode in objectsNode.Children())
                    {
                        if (childNode.DisplayName.Value.StartsWith("Device"))
                        {
                            try
                            {
                                string deviceId = childNode.DisplayName.Value;
                               OpcReadNode[] nodes = new OpcReadNode[]
                               {
                                   new OpcReadNode($"ns=2;s={deviceId}/ProductionStatus"),
                                   new OpcReadNode($"ns=2;s={deviceId}/ProductionRate"),
                                   new OpcReadNode($"ns=2;s={deviceId}/WorkorderId"),
                                   new OpcReadNode($"ns=2;s={deviceId}/Temperature"),
                                   new OpcReadNode($"ns=2;s={deviceId}/GoodCount"),
                                   new OpcReadNode($"ns=2;s={deviceId}/BadCount"),
                                   new OpcReadNode($"ns=2;s={deviceId}/DeviceError"),
                               };

                               var values = opcClient.ReadNodes(nodes).ToArray();

                               var data = new DeviceData
                               {
                                   ProductionStatus = (int)values[0].Value,
                                   ProductionRate = (int)values[1].Value,
                                   WorkerId = (string)values[2].Value,
                                   Temperature = (double)values[3].Value,
                                   GoodCount = (long)values[4].Value,
                                   BadCount = (long)values[5].Value,
                                   DeviceErrors = (int)values[6].Value
                               };                                       
                                   
                               await device.SendMessages(data, deviceId);
                               await device.UpdateTwinAsync(data, deviceId);

                               if (!prevErrors.TryGetValue(deviceId, out int previous) || previous != data.DeviceErrors)
                               {
                                   if (data.DeviceErrors > 0)
                                   {
                                       await device.SendDeviceErrorMessage(data.DeviceErrors, deviceId);
                                   }

                                   prevErrors[deviceId] = data.DeviceErrors;
                               }



                               Console.WriteLine($"{DateTime.Now:HH:mm:ss} - Device {childNode.NodeId.ToString()} data sent.\n");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ERROR] Device {childNode.NodeId.ToString()} OPC UA read/send failed: {ex.Message}");
                            }
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FATAL] Startup failed: {ex.Message}");
            }
        }

        internal class Configuration
        {
            public string DeviceConnectionString { get; set; }
        }
    }
}
