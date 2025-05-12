using Microsoft.Azure.Devices.Client;
using Device;
using Opc.UaFx.Client;
using Opc.UaFx;
namespace Agent
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            using (var client = new OpcClient("opc.tcp://localhost:4840/"))
            {
                client.Connect();

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


                IEnumerable<OpcValue> job = client.ReadNodes(nodes);

                DeviceData data = new DeviceData();
                data.ProductionStatus = (int)job.ToArray()[0].Value;
                data.ProductionRate = (int)job.ToArray()[1].Value;
                data.WorkerId = (string)job.ToArray()[2].Value;
                data.Temperature = (double)job.ToArray()[3].Value;
                data.GoodCount = (long)job.ToArray()[4].Value;
                data.BadCount = (long)job.ToArray()[5].Value;
                data.DeviceErrors = (int)job.ToArray()[6].Value;

                #region azure
                string deviceConnectionString = "";
                using var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString);
                await deviceClient.OpenAsync();

                var device = new VirtualDevice(deviceClient);

                await device.InitializeHandlers();
                await device.UpdateTwinAsync();
                Console.WriteLine("Connection Success!");

                await device.SendMessages(data);
                Console.WriteLine("Finished! Press Enter to close...");
                Console.ReadLine();
                #endregion

                //Console.WriteLine(node.Value);
            }
        
            
            
           
        }
    }
}
