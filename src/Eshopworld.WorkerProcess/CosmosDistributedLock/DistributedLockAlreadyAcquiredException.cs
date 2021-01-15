using System;

namespace EShopworld.WorkerProcess.CosmosDistributedLock
{
    public class DistributedLockAlreadyAcquiredException : Exception
    {
        public DistributedLockAlreadyAcquiredException(string lockName)
            : base($"Distributed Lock with name {lockName} was already acquired on the current object.")
        {
        }
    }
}