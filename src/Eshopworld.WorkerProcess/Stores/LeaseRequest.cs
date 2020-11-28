using Newtonsoft.Json;
using System;
using Microsoft.Azure.Documents;

namespace EShopworld.WorkerProcess.Stores
{
    public class LeaseRequest : Document
    {
        /// <inheritdoc />
        [JsonProperty("priority")]
        public int Priority { get; set; }
        /// <inheritdoc />
        [JsonProperty("instanceId")]
        public Guid? InstanceId { get; set; }
        /// <inheritdoc />
        [JsonProperty("leaseType")]
        public string LeaseType { get; set; }
    }
}
