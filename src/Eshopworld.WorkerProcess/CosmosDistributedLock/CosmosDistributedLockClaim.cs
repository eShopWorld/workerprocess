using Microsoft.Azure.Documents;

namespace EShopworld.WorkerProcess.CosmosDistributedLock
{
    /// <summary>
    /// Document stored as a claim to acquire a named lock with Document.Id = name of the lock
    /// </summary>
    public sealed class CosmosDistributedLockClaim : Resource, IDistributedLockClaim
    {
        public CosmosDistributedLockClaim(string id)
        {
            Id = id;
        }
    }
}