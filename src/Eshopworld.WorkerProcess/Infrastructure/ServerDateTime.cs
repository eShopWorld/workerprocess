using System;
using System.Diagnostics.CodeAnalysis;

namespace EShopworld.WorkerProcess.Infrastructure
{
    /// <summary>
    /// Helper class for date time resolution
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal static class ServerDateTime
    {
        /// <summary>
        /// The current UTC date time
        /// </summary>
        public static DateTime UtcNow => UtcNowFunc();

        /// <summary>
        /// The function used to resolve the date time
        /// </summary>
        public static Func<DateTime> UtcNowFunc { get; set; } = () => DateTime.UtcNow;
    }
}
