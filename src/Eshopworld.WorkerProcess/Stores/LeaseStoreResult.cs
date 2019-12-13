namespace EShopworld.WorkerProcess.Stores
{
    public class LeaseStoreResult
    {
        public LeaseStoreResult(ILease lease, bool result)
        {
            Lease = lease;
            Result = result;
        }

        public ILease Lease { get; }

        public bool Result { get; }
    }
}