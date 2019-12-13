using System;
using Microsoft.Azure.Documents;
using Newtonsoft.Json;

namespace EShopworld.WorkerProcess.Stores
{
    /// <summary>
    /// A cosmos db <see cref="ILease"/>
    /// </summary>
    internal class CosmosDbLease : Resource, ILease
    {
        /// <inheritdoc />
        [JsonProperty("priority")]
        public int Priority { get; set; }
        /// <inheritdoc />
        [JsonProperty("instanceId")]
        public Guid? InstanceId { get; set; }
        /// <inheritdoc />
        [JsonProperty("leasedUntil")]
        public DateTime? LeasedUntil { get; set; }
        /// <inheritdoc />
        [JsonProperty("leaseType")]
        public string LeaseType { get; set; }
    }
}
