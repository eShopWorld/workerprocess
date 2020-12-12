using System;

namespace EShopworld.WorkerProcess.Telemetry
{
    public class LeaseStoppedEvent : BaseLeaseEvent
    {
        public LeaseStoppedEvent(Guid instanceId, string leaseId, int priority) : base(instanceId, leaseId, priority, $"Instance {instanceId} was stopped")
        {
        }
    }
}
