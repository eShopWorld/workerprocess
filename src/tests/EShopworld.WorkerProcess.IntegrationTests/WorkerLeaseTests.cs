using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eshopworld.Core;
using Eshopworld.DevOps;
using Eshopworld.Telemetry;
using Eshopworld.Tests.Core;
using EShopworld.WorkerProcess.Configuration;
using EShopworld.WorkerProcess.Infrastructure;
using EShopworld.WorkerProcess.Stores;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace EShopworld.WorkerProcess.IntegrationTests
{
    [Collection("WorkerLeases")]
    public class WorkerLeaseTests
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly ITestOutputHelper _output;
        private readonly Dictionary<IWorkerLease, int> _allocatedList;
        private readonly List<IWorkerLease> _expiredList;
        private readonly List<IWorkerLease> _workerLeases;
        private readonly List<ManualResetEvent> _manualResetEvents;


        public WorkerLeaseTests(ITestOutputHelper output)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false, true)
                .Build();

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection, configuration);

            _serviceProvider = serviceCollection.BuildServiceProvider();

            _workerLeases = new List<IWorkerLease>();
            _allocatedList = new Dictionary<IWorkerLease, int>();
            _expiredList = new List<IWorkerLease>();
            _manualResetEvents = new List<ManualResetEvent>();
            _output = output;
        }

        [Fact, IsIntegration]
        public async Task TestSingleLease()
        {
            ManualResetEvent manualResetEvent = new ManualResetEvent(false);
            bool leaseAllocated = false;
            bool leaseExpired = false;

            // Arrange
            var leaseStore = _serviceProvider.GetService<ILeaseStore>();
            var workerLease = _serviceProvider.GetService<IWorkerLease>();
            
            // Act
            await leaseStore.InitialiseAsync();
            
            workerLease.LeaseAllocated += (sender, args) =>
            {
                _output.WriteLine($"[{DateTime.UtcNow}] Lease allocated until [{args.Expiry}]");

                leaseAllocated = true;
            };
            
            workerLease.LeaseExpired += (sender, args) =>
            {
                _output.WriteLine($"[{DateTime.UtcNow}] Lease expired");
                leaseExpired = true;
                manualResetEvent.Set();
            };

            workerLease.StartLeasing();

            manualResetEvent.WaitOne(new TimeSpan(0, 2, 30));

            workerLease.StopLeasing();

            // Assert
            leaseAllocated.Should().BeTrue();
            leaseExpired.Should().BeTrue();
        }

        [Fact, IsIntegration]
        public async Task TestMultipleWorkLeases()
        {
            // Arrange
            var leaseStore = _serviceProvider.GetService<ILeaseStore>();
            SetupWorkerLeases(3, i => i);

            Random r = new Random();

            // Act
            await leaseStore.InitialiseAsync();

            foreach (var workerLease in _workerLeases)
            {
                Thread.Sleep(new TimeSpan(0, 0, 1) * r.Next(10));

                workerLease.StartLeasing();

                _output.WriteLine($"Starting [{workerLease.InstanceId}]");
            }

            WaitHandle.WaitAny(_manualResetEvents.ToArray(), new TimeSpan(0, 2, 30));

            foreach (var workerLease in _workerLeases)
            {
                workerLease.StopLeasing();

                _output.WriteLine($"Stopping [{workerLease.InstanceId}]");
            }

            // Assert
            // Assert
            using (new AssertionScope())
            {
                _allocatedList.Should().HaveCount(1);
                _allocatedList.Select(wp => wp.Value).Should().AllBeEquivalentTo(0);
                _expiredList.Should().HaveCount(1);
            }
        }

        [Fact, IsIntegration]
        public async Task TestMultipleConcurrentWorkLeases()
        {
            // Arrange
            var leaseStore = _serviceProvider.GetService<ILeaseStore>();
            SetupWorkerLeases(30, i => WorkerLeaseOptions.MaxPriority - i % WorkerLeaseOptions.MaxPriority - 1);

            // Act
            await leaseStore.InitialiseAsync();

            foreach (var workerLease in _workerLeases)
            {
                workerLease.StartLeasing();

                _output.WriteLine($"[{DateTime.UtcNow}] Starting [{workerLease.InstanceId}]");
            }

            WaitHandle.WaitAny(_manualResetEvents.ToArray(), new TimeSpan(0, 2, 30));
            

            foreach (var workerLease in _workerLeases)
            {
                workerLease.StopLeasing();

                _output.WriteLine($"Stopping [{workerLease.InstanceId}]");
            }

            // Assert
            using (new AssertionScope())
            {
                _allocatedList.Should().HaveCount(1);
                _allocatedList.Select(wp => wp.Value).Should().AllBeEquivalentTo(0);
                _expiredList.Should().HaveCount(1);
            }
           
        }

        [Fact, IsIntegration]
        public async Task TestMultipleWorkLeasesSamePriority()
        {
           
            // Arrange
            var leaseStore = _serviceProvider.GetService<ILeaseStore>();

            SetupWorkerLeases(3, i => 0);

            Random r = new Random();

            // Act
            await leaseStore.InitialiseAsync();

            foreach (var workerLease in _workerLeases)
            {
                Thread.Sleep(new TimeSpan(0, 0, 1) * r.Next(10));

                workerLease.StartLeasing();

                _output.WriteLine($"Starting [{workerLease.InstanceId}]");
            }

            WaitHandle.WaitAny(_manualResetEvents.ToArray(), new TimeSpan(0, 2, 30));

            foreach (var workerLease in _workerLeases)
            {
                workerLease.StopLeasing();

                _output.WriteLine($"Stopping [{workerLease.InstanceId}]");
            }

            // Assert
            using (new AssertionScope())
            {
                _allocatedList.Should().HaveCount(1);
                _allocatedList.Select(wp => wp.Value).Should().AllBeEquivalentTo(0);
                _expiredList.Should().HaveCount(1);
            }
        }

        private void SetupWorkerLeases(int leaseCount, Func<int,int> assignPriorityFunc)
        {
            for (int i = 0; i < leaseCount; i++)
            {
                var options = Options.Create(new WorkerLeaseOptions
                {
                    LeaseInterval = new TimeSpan(0, 0, 30),
                    Priority = assignPriorityFunc(i),
                    WorkerType = "workertype"
                });

                var workerLease = CreateWorkerLease(options);
                var manualResetEvent = new ManualResetEvent(false);
                _manualResetEvents.Add(manualResetEvent);

                workerLease.LeaseAllocated += (sender, args) =>
                {
                    _output.WriteLine($"[{DateTime.UtcNow}] Lease allocated to [{workerLease.InstanceId}] Expiry: [{args.Expiry}]");

                    _allocatedList.Add((IWorkerLease)sender,options.Value.Priority);
                };

                workerLease.LeaseExpired += (sender, args) =>
                {
                    _output.WriteLine($"[{DateTime.UtcNow}] Lease expired for [{workerLease.InstanceId}]");

                    _expiredList.Add((IWorkerLease)sender);
                    manualResetEvent.Set();
                    manualResetEvent.Reset();
                };

                _workerLeases.Add(workerLease);
            }
        }
        private WorkerLease CreateWorkerLease(IOptions<WorkerLeaseOptions> options)
        {
            var telemetry = _serviceProvider.GetService<IBigBrother>();
            var leaseStore = _serviceProvider.GetService<ILeaseStore>();
            var slottedInterval = _serviceProvider.GetService<ISlottedInterval>();
            var timer= _serviceProvider.GetService<ITimer>();
            var leaseAllocator = new LeaseAllocator(telemetry, leaseStore, slottedInterval, options);

            var workerLease = new WorkerLease(telemetry, leaseAllocator,  timer, slottedInterval, options);
            return workerLease;
        }
        private void ConfigureServices(IServiceCollection services, IConfigurationRoot configuration)
        {
            services.AddOptions();
            services.Configure<CosmosDbConnectionOptions>(configuration.GetSection("CosmosDbConnectionOptions"));
            services.Configure<CosmosDataStoreOptions>(configuration.GetSection("CosmosDataStoreOptions"));
            services.Configure<WorkerLeaseOptions>(configuration.GetSection("WorkerLeaseOptions"));
            services.Configure<TelemetrySettings>(configuration.GetSection("Telemetry"));

            var serviceProvider = services.BuildServiceProvider();

            var telemetry = serviceProvider
                .GetService<IOptions<TelemetrySettings>>();

            var bb = new BigBrother(telemetry.Value.InstrumentationKey, telemetry.Value.InternalKey);

            services.AddSingleton<IBigBrother>(bb);

            var cosmosDbConnectionOptions = serviceProvider
                .GetService<IOptions<CosmosDbConnectionOptions>>();

            services.AddSingleton<IDocumentClient>(new DocumentClient(cosmosDbConnectionOptions.Value.ConnectionUri,
                cosmosDbConnectionOptions.Value.AccessKey));

            services.AddWorkerLease();
        }
    }
}