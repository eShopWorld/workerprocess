using System;

namespace EShopworld.WorkerProcess.Model
{
    public class LeaseRequest
    {
        public int Priority { get; set; }

        public Guid InstanceId { get; set; }

        public string LeaseType { get; set; }

        public int TimeToLive { get; set; }
    }
}
