using System;

namespace EShopworld.WorkerProcess.Telemetry
{
    /// <summary>
    /// An event that is raised when a lease is released
    /// </summary>
    public class LeaseReleaseEvent : BaseLeaseEvent
    {
        public LeaseReleaseEvent(Guid instanceId, string leaseId, int priority, string reason) : base(instanceId,
            leaseId, priority, reason)
        {
        }
    }
}