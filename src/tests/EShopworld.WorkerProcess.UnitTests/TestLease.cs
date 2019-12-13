using System;
using EShopworld.WorkerProcess.Stores;

namespace EShopworld.WorkerProcess.UnitTests
{
    public class TestLease : ILease
    {
        public string Id { get; set; }
        public int Priority { get; set; }
        public Guid? InstanceId { get; set; }
        public DateTime? LeasedUntil { get; set; }
        public string LeaseType { get; set; }
    }
}
