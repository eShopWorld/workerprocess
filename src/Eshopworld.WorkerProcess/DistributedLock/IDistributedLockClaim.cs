using System;
using System.Collections.Generic;
using System.Text;

namespace EShopworld.WorkerProcess.DistributedLock
{
    public interface IDistributedLockClaim
    {
        public string Id { get; set; }
    }
}
