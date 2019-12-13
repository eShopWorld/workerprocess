using System;

namespace EShopworld.WorkerProcess.Exceptions
{
    [Serializable]
    public class WorkerLeaseException : Exception
    {
        public WorkerLeaseException()
        {
        }

        public WorkerLeaseException(string message) : base(message)
        {
        }

        public WorkerLeaseException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
