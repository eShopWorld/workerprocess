using System;

namespace EShopworld.WorkerProcess
{
    public class LeaseAllocatedEventArgs : EventArgs
    {
        public LeaseAllocatedEventArgs(DateTime expiry)
        {
            Expiry = expiry;
        }

        public DateTime Expiry { get; }
    }
}