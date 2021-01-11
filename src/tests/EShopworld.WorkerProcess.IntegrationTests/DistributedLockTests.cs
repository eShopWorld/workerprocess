using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eshopworld.Core;
using Eshopworld.DevOps;
using Eshopworld.Telemetry;
using Eshopworld.Tests.Core;
using EShopworld.WorkerProcess.Configuration;
using EShopworld.WorkerProcess.CosmosDistributedLock;
using FluentAssertions;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace EShopworld.WorkerProcess.IntegrationTests
{
    public class DistributedLockTests
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly ITestOutputHelper _output;

        public DistributedLockTests(ITestOutputHelper output)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false, true)
                .Build();

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection, configuration);

            _serviceProvider = serviceCollection.BuildServiceProvider();

            _output = output;
        }

        [Fact, IsIntegration]
        public void AcquireLock_ForNewLock_ShouldNotThrowException()
        {
            var disLockStore = _serviceProvider.GetService<ICosmosDistributedLockStore>();
            Func<Task> act = async () =>
            {
                using (await new DistributedLock(disLockStore).Acquire("test1"))
                {
                    // create a recurring job
                }
            };

            act.Should().NotThrow();
        }

        [Fact, IsIntegration]
        public void AcquireLock_ForExistingLock_ShouldThrowException()
        {
            var disLockStore = _serviceProvider.GetService<ICosmosDistributedLockStore>();
            Func<Task> act = async () =>
            {
                using (await new DistributedLock(disLockStore).Acquire("test2"))
                {
                    using (await new DistributedLock(disLockStore).Acquire("test2"))
                    {
                        // create a recurring job
                    }
                }
            };

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

#pragma warning disable 618
            var bb = new BigBrother(telemetry.Value.InstrumentationKey, telemetry.Value.InternalKey);
#pragma warning restore 618

            services.AddSingleton<IBigBrother>(bb);

            var cosmosDbConnectionOptions = serviceProvider
                .GetService<IOptions<CosmosDbConnectionOptions>>();

            services.AddSingleton<IDocumentClient>(new DocumentClient(cosmosDbConnectionOptions.Value.ConnectionUri,
                cosmosDbConnectionOptions.Value.AccessKey));

            services.TryAddSingleton<ICosmosDistributedLockStore, CosmosDistributedLockStore>();

        }
    }
}
