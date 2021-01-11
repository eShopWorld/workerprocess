namespace EShopworld.WorkerProcess.CosmosDistributedLock
{
    public interface IDistributedLockClaim
    {
        public string Id { get; set; }
    }
}
