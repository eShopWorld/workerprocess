using System.Threading.Tasks;

namespace EShopworld.WorkerProcess.CosmosDistributedLock
{
    public interface ICosmosDistributedLockStore
    {
        Task InitialiseAsync();
        Task<bool> TryClaimLockAsync(IDistributedLockClaim claim);
        Task ReleaseLockAsync(IDistributedLockClaim claim);
    }
}