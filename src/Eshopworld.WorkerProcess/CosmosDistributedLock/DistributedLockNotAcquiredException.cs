using System;

namespace EShopworld.WorkerProcess.CosmosDistributedLock
{
    public class DistributedLockNotAcquiredException : Exception
    {
        public DistributedLockNotAcquiredException(string lockName, Exception innerException)
            : base($"Distributed Lock for document with id: {lockName} could not be acquired.", innerException)
        {
        }
    }
}