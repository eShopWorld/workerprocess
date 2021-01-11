using Microsoft.Azure.Documents;

namespace EShopworld.WorkerProcess.DistributedLock
{
    public sealed class DistributedLockClaim : Resource, IDistributedLockClaim
    {
        public DistributedLockClaim(string id)
        {
            Id = id;
        }
    }
}