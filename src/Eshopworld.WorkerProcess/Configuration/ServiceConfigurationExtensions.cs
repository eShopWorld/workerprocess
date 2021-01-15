using System.Diagnostics.CodeAnalysis;
using Autofac;
using Eshopworld.Core;
using EShopworld.WorkerProcess.CosmosDistributedLock;
using EShopworld.WorkerProcess.Exceptions;
using EShopworld.WorkerProcess.Infrastructure;
using EShopworld.WorkerProcess.Stores;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EShopworld.WorkerProcess.Configuration
{
    [ExcludeFromCodeCoverage]
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

            services.TryAddTransient<ITimer, SystemTimer>();

            services.TryAddSingleton<IWorkerLease, WorkerLease>();
        }

        /// <summary>
        /// Adds <see cref="DistributedLock"/> to the services collection
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="cosmosDataStoreOptions"></param>
        /// <param name="cosmosDbConnectionOptions"></param>
        public static void AddCosmosDistributedLock(this ContainerBuilder builder, CosmosDataStoreOptions cosmosDataStoreOptions, CosmosDbConnectionOptions cosmosDbConnectionOptions)
        {
            builder.Register(ctx => Options.Create(cosmosDataStoreOptions))
                .As<IOptions<CosmosDataStoreOptions>>().SingleInstance();

            builder.Register(
                ctx => new DocumentClient(cosmosDbConnectionOptions.ConnectionUri, cosmosDbConnectionOptions.AccessKey)
            ).Named<IDocumentClient>("WorkerProcessDocumentClient");

            builder.Register(ctx =>
                new CosmosDistributedLockStore(
                    ctx.ResolveNamed<IDocumentClient>("WorkerProcessDocumentClient"),
                    ctx.Resolve<IOptions<CosmosDataStoreOptions>>(),
                    ctx.Resolve<IBigBrother>())).As<ICosmosDistributedLockStore>().SingleInstance();

            builder.RegisterType<DistributedLock>().As<IDistributedLock>();
        }
    }
}