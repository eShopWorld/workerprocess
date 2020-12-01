using EShopworld.WorkerProcess.Stores;
using System;

namespace EShopworld.WorkerProcess.Model
{
    public class Lease : ILease
    {
        /// <inheritdoc />
        public string Id { get; set; }
        /// <inheritdoc />
        public int Priority { get; set; }
        /// <inheritdoc />
        public Guid? InstanceId { get; set; }
        /// <inheritdoc />
        public DateTime? LeasedUntil { get; set; }
        public TimeSpan? Interval { get; set; }
        /// <inheritdoc />
        public string LeaseType { get; set; }
    }
}
