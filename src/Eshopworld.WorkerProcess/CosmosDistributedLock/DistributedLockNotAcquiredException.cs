using System;

namespace EShopworld.WorkerProcess.CosmosDistributedLock
{
    public class DistributedLockNotAcquiredException : Exception
    {
        public DistributedLockNotAcquiredException(string lockName, string collectionName, Exception innerException)
            : base($"Distributed Lock for document with id: '{lockName}' in collection '{collectionName}' could not be acquired.", innerException)
        {
        }
    }
}