// Program.cs
using Microsoft.Azure.Devices.Client;
using Device;
using Opc.UaFx.Client;
using Opc.UaFx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Agent
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            string deviceConnectionString = "";
            using var opcClient = new OpcClient("opc.tcp://localhost:4840/");
            using var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString);

            try
            {
                opcClient.Connect();
                await deviceClient.OpenAsync();

                var device = new VirtualDevice(deviceClient, opcClient);
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
                                Console.WriteLine(childNode.DisplayName.Value);
                                OpcReadNode[] nodes = new OpcReadNode[]
                                {
                                new OpcReadNode($"ns=2;s={childNode.DisplayName.Value}/ProductionStatus"),
                                new OpcReadNode($"ns=2;s={childNode.DisplayName.Value}/ProductionRate"),
                                new OpcReadNode($"ns=2;s={childNode.DisplayName.Value}/WorkorderId"),
                                new OpcReadNode($"ns=2;s={childNode.DisplayName.Value}/Temperature"),
                                new OpcReadNode($"ns=2;s={childNode.DisplayName.Value}/GoodCount"),
                                new OpcReadNode($"ns=2;s={childNode.DisplayName.Value}/BadCount"),
                                new OpcReadNode($"ns=2;s={childNode.DisplayName.Value}/DeviceError"),
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

                                await device.SendMessages(data);
                                await device.UpdateTwinAsync(data);

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
    }
}
