using System.Threading.Tasks;

namespace EShopworld.WorkerProcess.CosmosDistributedLock
{
    public interface ICosmosDistributedLockStore
    {
        Task<bool> TryClaimLockAsync(IDistributedLockClaim claim);
        Task ReleaseLockAsync(IDistributedLockClaim claim);
    }
}