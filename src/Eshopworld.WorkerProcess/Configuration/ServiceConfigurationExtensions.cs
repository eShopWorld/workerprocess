using EShopworld.WorkerProcess.Exceptions;
using EShopworld.WorkerProcess.Infrastructure;
using EShopworld.WorkerProcess.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EShopworld.WorkerProcess.Configuration
{
    public static class ServiceConfigurationExtensions
    {
        /// <summary>
        ///     Adds a <see cref="IWorkerLease"/> to the services collection
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/></param>
        public static void AddWorkerLease(this IServiceCollection services)
        {
            var serviceProvider = services.BuildServiceProvider();

            var options = serviceProvider
                .GetService<IOptionsMonitor<WorkerLeaseOptions>>();

            if (options.CurrentValue == null)
                throw new WorkerLeaseException($"[{typeof(IOptionsMonitor<WorkerLeaseOptions>)}] was not configured");

            services.TryAddSingleton<ISlottedInterval, SlottedInterval>();

            services.TryAddSingleton<ILeaseAllocator, LeaseAllocator>();

            services.TryAddSingleton<ILeaseStore, CosmosDbLeaseStore>();

            services.TryAddSingleton<ITimer, SystemTimer>();

            services.TryAddSingleton<IWorkerLease, WorkerLease>();
        }
    }
}