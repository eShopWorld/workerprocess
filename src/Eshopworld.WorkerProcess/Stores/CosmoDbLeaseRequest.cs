using System;
using Microsoft.Azure.Documents;
using Newtonsoft.Json;

namespace EShopworld.WorkerProcess.Stores
{
    internal class CosmoDbLeaseRequest : Document
    {
        [JsonProperty("priority")]
        public int Priority { get; set; }

        [JsonProperty("instanceId")]
        public Guid InstanceId { get; set; }

        [JsonProperty("leaseType")]
        public string LeaseType { get; set; }
    }
}
