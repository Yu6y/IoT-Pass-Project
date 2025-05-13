using Microsoft.Azure.Devices.Client;
using Opc.UaFx.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Device
{
    public class DeviceData
    {
        public int ProductionStatus { get; set; }
        public string WorkerId { get; set; }
        public int ProductionRate { get; set; }
        public long GoodCount { get; set; }
        public long BadCount { get; set; }
        public double Temperature { get; set; }
        public int DeviceErrors { get; set; }

        public DeviceData()
        {

        }

        public DeviceData(int productionStatus, string workerId, int productionRate, int goodCount, int badCount, double temperature, int deviceErrors)
        {
            ProductionStatus = productionStatus;
            WorkerId = workerId;
            ProductionRate = productionRate;
            GoodCount = goodCount;
            BadCount = badCount;
            Temperature = temperature;
            DeviceErrors = deviceErrors;
        }
    }
}
