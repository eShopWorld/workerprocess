using System;
using System.IO;
using System.Threading.Tasks;
using Eshopworld.Core;
using Eshopworld.DevOps;
using Eshopworld.Telemetry;
using Eshopworld.Tests.Core;
using EShopworld.WorkerProcess.Configuration;
using EShopworld.WorkerProcess.CosmosDistributedLock;
using FluentAssertions;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EShopworld.WorkerProcess.IntegrationTests
{
    public class DistributedLockTests
    {
        private readonly ServiceProvider _serviceProvider;

        public DistributedLockTests()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false, true)
                .Build();

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection, configuration);

            _serviceProvider = serviceCollection.BuildServiceProvider();
        }

        [Fact, IsIntegration]
        public void AcquireLock_ForNewLock_ShouldNotThrowException()
        {
            // Arrange
            Func<Task> act = async () =>
            {
                var distributedLock = _serviceProvider.GetService<IDistributedLock>();
                using (await distributedLock.Acquire("test1"))
                {
                    // create a recurring job
                }
            };

            // Act - Assert
            act.Should().NotThrow();
        }

        [Fact, IsIntegration]
        public void AcquireLock_ForExistingLock_ShouldThrowException()
        {
            // Arrange
            Func<Task> act = async () =>
            {
                var distributedLock = _serviceProvider.GetService<IDistributedLock>();
                using (await distributedLock.Acquire("test2"))
                {
                    using (await distributedLock.Acquire("test2"))
                    {
                        // create a recurring job
                    }
                }
            };

            // Act - Assert
            act.Should().Throw<DistributedLockNotAcquiredException>();
        }

        private void ConfigureServices(IServiceCollection services, IConfigurationRoot configuration)
        {
            services.AddOptions();
            services.Configure<CosmosDbConnectionOptions>(configuration.GetSection("CosmosDbConnectionOptions"));
            services.Configure<CosmosDataStoreOptions>(configuration.GetSection("CosmosDataStoreOptions"));
            services.Configure<TelemetrySettings>(configuration.GetSection("Telemetry"));


            var serviceProvider = services.BuildServiceProvider();

            var telemetry = serviceProvider
                .GetService<IOptions<TelemetrySettings>>();

            var bb = BigBrother.CreateDefault(telemetry.Value.InstrumentationKey, telemetry.Value.InternalKey);

            services.AddSingleton<IBigBrother>(bb);

            var cosmosDbConnectionOptions = serviceProvider
                .GetService<IOptions<CosmosDbConnectionOptions>>();

            services.AddSingleton<IDocumentClient>(new DocumentClient(cosmosDbConnectionOptions.Value.ConnectionUri,
                cosmosDbConnectionOptions.Value.AccessKey));

            services.TryAddSingleton<ICosmosDistributedLockStore, CosmosDistributedLockStore>();
            services.TryAddTransient<IDistributedLock, DistributedLock>();

        }
    }
}
