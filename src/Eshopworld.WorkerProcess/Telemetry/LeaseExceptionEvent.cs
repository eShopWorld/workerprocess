using Eshopworld.Core;

namespace EShopworld.WorkerProcess.Telemetry
{
    /// <summary>
    /// An event that is raised when an exception occurs when an exception occurs during the leasing process
    /// </summary>
    internal class LeaseExceptionEvent : ExceptionEvent
    {
        /// <summary>
        ///     Create instance of <see cref="LeaseExceptionEvent" />
        /// </summary>
        /// <param name="exception"></param>
        public LeaseExceptionEvent(System.Exception exception) : base(exception)
        {
        }
    }
}