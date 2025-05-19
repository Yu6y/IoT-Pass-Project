using Newtonsoft.Json;

namespace Device
{
    [JsonObject(MemberSerialization.OptIn)]
    public class DeviceData
    {
        [JsonProperty]
        public string DeviceId { get; set; }
        [JsonProperty]
        public int ProductionStatus { get; set; }
        [JsonProperty]
        public string WorkerId { get; set; }        
        public int ProductionRate { get; set; }
        [JsonProperty]
        public long GoodCount { get; set; }
        [JsonProperty]
        public long BadCount { get; set; }
        [JsonProperty]
        public double Temperature { get; set; }
        public int DeviceErrors { get; set; }

        public DeviceData()
        {

        }

        public DeviceData(string deviceId, int productionStatus, string workerId, int productionRate, int goodCount, int badCount, double temperature, int deviceErrors)
        {
            DeviceId = deviceId;
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
