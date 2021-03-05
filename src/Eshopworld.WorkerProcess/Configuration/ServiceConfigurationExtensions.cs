using System.Diagnostics.CodeAnalysis;
using Autofac;
using Eshopworld.Core;
using EShopworld.WorkerProcess.CosmosDistributedLock;
using EShopworld.WorkerProcess.Exceptions;
using EShopworld.WorkerProcess.Infrastructure;
using EShopworld.WorkerProcess.Stores;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EShopworld.WorkerProcess.Configuration
{
    [ExcludeFromCodeCoverage]
    public static class ServiceConfigurationExtensions
    {
        private const string WorkerLeaseOptions = "WorkerProcess:WorkerLease";
        private const string CosmosDataStoreOptions = "WorkerProcess:CosmosDataStore";
        private const string CosmosConnectionKeVaultKey = "cm--cosmos-connection--worker-process";

        /// <summary>
        ///  Adds a <see cref="IWorkerLease"/> to the services collection
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/></param>
        /// <param name="configuration">The <see cref="IConfigurationRoot"/></param>
        public static void AddWorkerLease(this IServiceCollection services, IConfigurationRoot configuration)
        {
            services.AddOptions();
            services.Configure<WorkerLeaseOptions>(configuration.GetSection(WorkerLeaseOptions));

            var cosmosDataStoreOptions = configuration.BindSection<CosmosDataStoreOptions>(CosmosDataStoreOptions,
                m => { m.AddMapping(x => x.ConnectionString, CosmosConnectionKeVaultKey); });

            services.TryAddSingleton<ISlottedInterval, SlottedInterval>();

            services.TryAddSingleton<ILeaseAllocator, LeaseAllocator>();

            services.TryAddSingleton<ITimer, SystemTimer>();

            services.TryAddSingleton(CreateLeaseStore(services, cosmosDataStoreOptions));

            services.TryAddSingleton<IWorkerLease, WorkerLease>();
        }

        /// <summary>
        /// Adds <see cref="DistributedLock"/> to the services collection
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        public static void AddCosmosDistributedLock(this IServiceCollection services, IConfigurationRoot configuration)
        {
            var cosmosDataStoreOptions = configuration.BindSection<CosmosDataStoreOptions>(CosmosDataStoreOptions,
                m => { m.AddMapping(x => x.ConnectionString, CosmosConnectionKeVaultKey); });

            services.TryAddSingleton(Options.Create(cosmosDataStoreOptions));
            services.TryAddSingleton(CreateDistributedLockStore(services,cosmosDataStoreOptions));
            services.TryAddTransient<IDistributedLock,DistributedLock>();
        }

        private static ICosmosDistributedLockStore CreateDistributedLockStore(IServiceCollection services,
            CosmosDataStoreOptions cosmosDataStoreOptions)
        {
            var serviceProvider = services.BuildServiceProvider();
            var cosmosDbConnectionString = new CosmosDbConnectionString(cosmosDataStoreOptions.ConnectionString);
            var documentClient = new DocumentClient(cosmosDbConnectionString.ServiceEndpoint, cosmosDbConnectionString.AuthKey);
            return new CosmosDistributedLockStore(
                documentClient,
                serviceProvider.GetService<IOptions<CosmosDataStoreOptions>>(),
                serviceProvider.GetService<IBigBrother>());
        }


        private static ILeaseStore CreateLeaseStore(IServiceCollection services, CosmosDataStoreOptions cosmosDataStoreOptions)
        {
            var cosmosDbConnectionString = new CosmosDbConnectionString(cosmosDataStoreOptions.ConnectionString);
            var documentClient = new DocumentClient(cosmosDbConnectionString.ServiceEndpoint, cosmosDbConnectionString.AuthKey);
            var serviceProvider = services.BuildServiceProvider();
            return new CosmosDbLeaseStore(documentClient, serviceProvider.GetService<IBigBrother>(),
                Options.Create(cosmosDataStoreOptions));
        }


    }
}