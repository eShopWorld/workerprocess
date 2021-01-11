using System;

namespace EShopworld.WorkerProcess.DistributedLock
{
    public class DistributedLockNotAcquiredException : Exception
    {
        public DistributedLockNotAcquiredException(string lockName, Exception innerException)
            : base($"Distributed Lock for document with id: {lockName} could not be acquired.", innerException)
        {
        }
    }
}