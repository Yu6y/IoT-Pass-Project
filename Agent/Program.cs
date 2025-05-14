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
            string deviceConnectionString = "HostName=IoTZajecia.azure-devices.net;DeviceId=test_device;SharedAccessKey=g/tfP0og9Qm3vGzht2vGFEA1T7volalBQBXPickK9SU=";

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
                    try
                    {
                        OpcReadNode[] nodes = new OpcReadNode[]
                        {
                            new OpcReadNode("ns=2;s=Device 1/ProductionStatus"),
                            new OpcReadNode("ns=2;s=Device 1/ProductionRate"),
                            new OpcReadNode("ns=2;s=Device 1/WorkorderId"),
                            new OpcReadNode("ns=2;s=Device 1/Temperature"),
                            new OpcReadNode("ns=2;s=Device 1/GoodCount"),
                            new OpcReadNode("ns=2;s=Device 1/BadCount"),
                            new OpcReadNode("ns=2;s=Device 1/DeviceError"),
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

                        Console.WriteLine($"{DateTime.Now:HH:mm:ss} - Data sent.\n");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] OPC UA read/send failed: {ex.Message}");
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