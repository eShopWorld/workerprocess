using System;

namespace EShopworld.WorkerProcess.Telemetry
{
    /// <summary>
    /// An event that is raised when a lease event occurs
    /// </summary>
    public class LeaseAcquisitionEvent : BaseLeaseEvent
    {
        public LeaseAcquisitionEvent(Guid instanceId, string leaseId, int priority, string reason) : base(instanceId,
            leaseId, priority, reason)
        {
        }
    }
}