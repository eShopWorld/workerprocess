using System;
using System.Collections.Concurrent;
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
        private readonly ConcurrentDictionary<IWorkerLease, int> _workerLeases;
        private readonly ManualResetEvent _leaseAllocatedEvent;
        private readonly ManualResetEvent _leaseExpiredEvent;
        private readonly ConcurrentDictionary<IWorkerLease, int> _allocatedList;
        private readonly ConcurrentBag<IWorkerLease> _expiredList;
        private readonly object _lock = new object();

        public WorkerLeaseTests(ITestOutputHelper output)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false, true)
                .Build();

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection, configuration);

            _serviceProvider = serviceCollection.BuildServiceProvider();

            _workerLeases = new ConcurrentDictionary<IWorkerLease, int>();
            _leaseAllocatedEvent = new ManualResetEvent(false);
            _leaseExpiredEvent = new ManualResetEvent(false);
            _allocatedList = new ConcurrentDictionary<IWorkerLease, int>();
            _expiredList = new ConcurrentBag<IWorkerLease>();
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
            var workerLease = CreateWorkerLease(Options.Create(new WorkerLeaseOptions
            {
                LeaseInterval = new TimeSpan(0, 0, 30),
                Priority = 1,
                WorkerType = "TestSingleLease",

            }));

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
            SetupWorkerLeases(3, i => i, "TestMultipleWorkLeases");

            Random r = new Random();

            // Act
            await leaseStore.InitialiseAsync();

            foreach (var (workerLease, _) in _workerLeases)
            {
                Thread.Sleep(new TimeSpan(0, 0, 1) * r.Next(10));

                workerLease.StartLeasing();

                _output.WriteLine($"Starting [{workerLease.InstanceId}]");
            }

            _leaseExpiredEvent.WaitOne(new TimeSpan(0, 2, 30));

            foreach (var (workerLease, _) in _workerLeases)
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
        public async Task TestMultipleConcurrentWorkLeases()
        {
            // Arrange
            var leaseStore = _serviceProvider.GetService<ILeaseStore>();
            SetupWorkerLeases(30, i => WorkerLeaseOptions.MaxPriority - i % WorkerLeaseOptions.MaxPriority - 1, "TestMultipleConcurrentWorkLeases");

            // Act
            await leaseStore.InitialiseAsync();

            foreach (var (workerLease, _) in _workerLeases)
            {
                workerLease.StartLeasing();

                _output.WriteLine($"[{DateTime.UtcNow}] Starting [{workerLease.InstanceId}]");
            }

            _leaseExpiredEvent.WaitOne(new TimeSpan(0, 2, 30));


            foreach (var (workerLease, _) in _workerLeases)
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

            SetupWorkerLeases(3, i => 0, "TestMultipleWorkLeasesSamePriority");

            Random r = new Random();

            // Act
            await leaseStore.InitialiseAsync();

            foreach (var (workerLease, _) in _workerLeases)
            {
                Thread.Sleep(new TimeSpan(0, 0, 1) * r.Next(10));

                workerLease.StartLeasing();

                _output.WriteLine($"Starting [{workerLease.InstanceId}]");
            }

            _leaseExpiredEvent.WaitOne(new TimeSpan(0, 2, 30));

            foreach (var (workerLease, _) in _workerLeases)
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
        public async Task TestWorkLease_WhenLowerPriorityStartsLeaseBeforeHigherPriority_TheHighestPriorityShouldAlwaysWin()
        {
            //arrange
            ConcurrentBag<IWorkerLease> expired = new ConcurrentBag<IWorkerLease>();
            ManualResetEvent manualResetEvent = new ManualResetEvent(false);
            var leaseStore = _serviceProvider.GetService<ILeaseStore>();

            Guid? winner = null;
            void OnWorkerProcessOnLeaseAllocated(object sender, LeaseAllocatedEventArgs args)
            {
                
                lock (_lock)
                {
                    var wp = sender as WorkerLease;
                    winner = wp.InstanceId;
                }
            }

            void OnWorkerProcessOnLeaseExpired(object sender, EventArgs args)
            {
                var wp = sender as WorkerLease;
                manualResetEvent.Set();
                expired.Add(wp);
            }
            var workerType = "TestWorkLease_WhenLowerPriorityStartsLeaseBeforeHigherPriority_TheHighestPriorityShouldAlwaysWin";
            var lowPriorityWorkerProcess = CreateWorkerLease(Options.Create(new WorkerLeaseOptions
            {
                LeaseInterval = new TimeSpan(0, 0, 30),
                Priority = 1,
                WorkerType = workerType,

            }));


            var highPriorityWorkerProcess = CreateWorkerLease(Options.Create(new WorkerLeaseOptions
            {
                LeaseInterval = new TimeSpan(0, 0, 30),
                Priority = 0,
                WorkerType = workerType
            }));

            lowPriorityWorkerProcess.LeaseAllocated += OnWorkerProcessOnLeaseAllocated;
            highPriorityWorkerProcess.LeaseAllocated += OnWorkerProcessOnLeaseAllocated;

            lowPriorityWorkerProcess.LeaseExpired += OnWorkerProcessOnLeaseExpired;
            highPriorityWorkerProcess.LeaseExpired += OnWorkerProcessOnLeaseExpired;

            //Act
            await leaseStore.InitialiseAsync();

            lowPriorityWorkerProcess.StartLeasing();
            await Task.Delay(500);
            highPriorityWorkerProcess.StartLeasing();
            manualResetEvent.WaitOne(TimeSpan.FromMinutes(2));

            //Assert
            winner.Should().Be(highPriorityWorkerProcess.InstanceId);
            expired.Should().HaveCount(1);
        }

        [Fact, IsIntegration]
        public async Task TestWorkLease_WhenHigherPriorityStartsLeaseBeforeLowerPriority_TheHighestPriorityShouldAlwaysWin()
        {
            //arrange
            ConcurrentBag<IWorkerLease> expired = new ConcurrentBag<IWorkerLease>();
            ManualResetEvent manualResetEvent = new ManualResetEvent(false);
            var leaseStore = _serviceProvider.GetService<ILeaseStore>();

            Guid? winner = null;
            void OnWorkerProcessOnLeaseAllocated(object sender, LeaseAllocatedEventArgs args)
            {
                
                lock (_lock)
                {
                    var wp = sender as WorkerLease;
                    winner = wp.InstanceId;
                }
            }

            void OnWorkerProcessOnLeaseExpired(object sender, EventArgs args)
            {
                var wp = sender as WorkerLease;
                manualResetEvent.Set();
                expired.Add(wp);
            }

            var workerType = "TestWorkLease_WhenHigherPriorityStartsLeaseBeforeLowerPriority_TheHighestPriorityShouldAlwaysWin";
            var lowPriorityWorkerProcess = CreateWorkerLease(Options.Create(new WorkerLeaseOptions
            {
                LeaseInterval = new TimeSpan(0, 0, 30),
                Priority = 1,
                WorkerType = workerType

            }));


            var highPriorityWorkerProcess = CreateWorkerLease(Options.Create(new WorkerLeaseOptions
            {
                LeaseInterval = new TimeSpan(0, 0, 30),
                Priority = 0,
                WorkerType = workerType
            }));

            lowPriorityWorkerProcess.LeaseAllocated += OnWorkerProcessOnLeaseAllocated;
            highPriorityWorkerProcess.LeaseAllocated += OnWorkerProcessOnLeaseAllocated;

            lowPriorityWorkerProcess.LeaseExpired += OnWorkerProcessOnLeaseExpired;
            highPriorityWorkerProcess.LeaseExpired += OnWorkerProcessOnLeaseExpired;

            //Act
            await leaseStore.InitialiseAsync();

            highPriorityWorkerProcess.StartLeasing();
            await Task.Delay(500);
            lowPriorityWorkerProcess.StartLeasing();
            manualResetEvent.WaitOne(TimeSpan.FromMinutes(2));

            //Assert
            winner.Should().Be(highPriorityWorkerProcess.InstanceId);
            expired.Should().HaveCount(1);
        }

        [Fact, IsIntegration]
        public async Task TestWorkLease_WhenAWpWithDifferentInstanceAndSamePriorityThanCurrentLeaseHolderStartsALeaseDuringLeaseTime_ShouldLose()
        {
            //arrange
            ManualResetEvent manualResetEvent = new ManualResetEvent(false);
            var leaseStore = _serviceProvider.GetService<ILeaseStore>();

            Guid? winner = null;
            var workerType = "TestWorkLease_WhenAWpWithDifferentInstanceAndSamePriorityThanCurrentLeaseHolderStartsALeaseDuringLeaseTime_ShouldLose";
            var options = Options.Create(new WorkerLeaseOptions
            {
                LeaseInterval = new TimeSpan(0, 0, 30),
                Priority = 0,
                WorkerType = workerType

            });

            var leaseHolder = CreateWorkerLease(options);


            void OnWorkerProcessOnLeaseAllocated(object sender, LeaseAllocatedEventArgs args)
            {
                
                lock (_lock)
                {
                    var wp = sender as WorkerLease;
                    winner = wp.InstanceId;
                }
                manualResetEvent.Set();
                manualResetEvent.Reset();
                //While the lease is acquired another process tries to acquire the lease in between
                options.Value.LeaseInterval = TimeSpan.FromSeconds(args.Expiry.Subtract(DateTime.UtcNow).TotalSeconds / 2);
                var laterWorkerProcess = CreateWorkerLease(options);
                laterWorkerProcess.StartLeasing();
                laterWorkerProcess.LeaseAllocated += (sender, eventArgs) =>
                {
                    //It should not get here
                    lock (_lock)
                    {
                        winner = (sender as WorkerLease).InstanceId;
                    }
                };
            }

            leaseHolder.LeaseAllocated += OnWorkerProcessOnLeaseAllocated;

            //Act
            await leaseStore.InitialiseAsync();

            leaseHolder.StartLeasing();
            manualResetEvent.WaitOne(TimeSpan.FromMinutes(2));
            await Task.Delay(500);
            //Give the other Process the chance to acquire the lease
            manualResetEvent.WaitOne(TimeSpan.FromSeconds(30));
            //Assert
            //The other process should not acquire the lease
            winner.Should().Be(leaseHolder.InstanceId);

        }

        [Fact, IsIntegration]
        public async Task TestWorkLease_WhenTwoWpHaveSameInstanceIdAndSamePriority_TheyShouldBothWin()
        {
            //arrange
            ConcurrentBag<IWorkerLease> expired = new ConcurrentBag<IWorkerLease>();
            Dictionary<IWorkerLease, ManualResetEvent> manualResetEventsDictionary = new Dictionary<IWorkerLease, ManualResetEvent>();
            ConcurrentBag<Guid> winners = new ConcurrentBag<Guid>();
            var leaseStore = _serviceProvider.GetService<ILeaseStore>();

            void OnWorkerProcessOnLeaseAllocated(object sender, LeaseAllocatedEventArgs args)
            {
                var wp = sender as WorkerLease;
                winners.Add(wp.InstanceId);
            }
            void OnWorkerProcessOnLeaseExpired(object sender, EventArgs args)
            {
                var wp = sender as WorkerLease;
                manualResetEventsDictionary[wp].Set();
                expired.Add(wp);
            }

            var instanceId = Guid.NewGuid();
            var options = Options.Create(new WorkerLeaseOptions
            {
                LeaseInterval = new TimeSpan(0, 0, 30),
                Priority = 0,
                WorkerType = "TestWorkLease_WhenTwoWpHaveSameInstanceIdAndSamePriority_TheyShouldBothWin",
                InstanceId = instanceId

            });
            var workerProcess1 = CreateWorkerLease(options);
            manualResetEventsDictionary[workerProcess1] = new ManualResetEvent(false);
            var workerProcess2 = CreateWorkerLease(options);
            manualResetEventsDictionary[workerProcess2] = new ManualResetEvent(false);

            workerProcess1.LeaseAllocated += OnWorkerProcessOnLeaseAllocated;
            workerProcess2.LeaseAllocated += OnWorkerProcessOnLeaseAllocated;

            workerProcess1.LeaseExpired += OnWorkerProcessOnLeaseExpired;
            workerProcess2.LeaseExpired += OnWorkerProcessOnLeaseExpired;


            //Act
            await leaseStore.InitialiseAsync();

            workerProcess2.StartLeasing();
            await Task.Delay(500);
            workerProcess1.StartLeasing();
            WaitHandle.WaitAll(manualResetEventsDictionary.Values.ToArray(), TimeSpan.FromMinutes(1));

            //Assert
            winners.Should().HaveCount(2);
            winners.Should().AllBeEquivalentTo(instanceId);
            expired.Should().HaveCount(2);
        }

        [Fact, IsIntegration]
        public async Task TestWorkLease_WhenTwoWpHaveSameInstanceIdAndDifferentPriority_TheyShouldBothWin()
        {
            //arrange
            ConcurrentBag<IWorkerLease> expired = new ConcurrentBag<IWorkerLease>();
            Dictionary<IWorkerLease, ManualResetEvent> manualResetEventsDictionary = new Dictionary<IWorkerLease, ManualResetEvent>();
            ConcurrentBag<Guid> winners = new ConcurrentBag<Guid>();
            var leaseStore = _serviceProvider.GetService<ILeaseStore>();

            void OnWorkerProcessOnLeaseAllocated(object sender, LeaseAllocatedEventArgs args)
            {
                var wp = sender as WorkerLease;
                winners.Add(wp.InstanceId);
            }
            void OnWorkerProcessOnLeaseExpired(object sender, EventArgs args)
            {
                var wp = sender as WorkerLease;
                manualResetEventsDictionary[wp].Set();
                expired.Add(wp);
            }

            var instanceId = Guid.NewGuid();
            var workerProcess1 = CreateWorkerLease(Options.Create(new WorkerLeaseOptions
            {
                LeaseInterval = new TimeSpan(0, 0, 30),
                Priority = 0,
                WorkerType = "TestWorkLease_WhenTwoWpHaveSameInstanceIdAndDifferentPriority_TheyShouldBothWin",
                InstanceId = instanceId

            }));
            manualResetEventsDictionary[workerProcess1] = new ManualResetEvent(false);
            var workerProcess2 = CreateWorkerLease(Options.Create(new WorkerLeaseOptions
            {
                LeaseInterval = new TimeSpan(0, 0, 30),
                Priority = 1,
                WorkerType = "TestWorkLease_WhenTwoWpHaveSameInstanceIdAndDifferentPriority_TheyShouldBothWin",
                InstanceId = instanceId

            }));
            manualResetEventsDictionary[workerProcess2] = new ManualResetEvent(false);

            workerProcess1.LeaseAllocated += OnWorkerProcessOnLeaseAllocated;
            workerProcess2.LeaseAllocated += OnWorkerProcessOnLeaseAllocated;

            workerProcess1.LeaseExpired += OnWorkerProcessOnLeaseExpired;
            workerProcess2.LeaseExpired += OnWorkerProcessOnLeaseExpired;


            //Act
            await leaseStore.InitialiseAsync();

            workerProcess2.StartLeasing();
            await Task.Delay(500);
            workerProcess1.StartLeasing();
            WaitHandle.WaitAll(manualResetEventsDictionary.Values.ToArray(), TimeSpan.FromMinutes(1));

            //Assert
            winners.Should().HaveCount(2);
            winners.Should().AllBeEquivalentTo(instanceId);
            expired.Should().HaveCount(2);
        }

        [Fact, IsIntegration]
        public async Task WorkerLease_MultipleWorkersPerInstanceId_LeaseAllocatedToAllWorkersWithSameInstanceId()
        {
            // Arrange
            var leaseStore = _serviceProvider.GetService<ILeaseStore>();
            for (var i = 0; i < 3; i++)
            {
                var iPriority = i;
                // setup 3 workers for each priority
                SetupWorkerLeases(3, _ => iPriority, "WorkerLease_MultipleWorkersPerInstanceId_LeaseAllocatedToAllWorkersWithSameInstanceId", Guid.NewGuid());
            }

            // Act
            await leaseStore.InitialiseAsync();

            Parallel.ForEach(_workerLeases, (workerLease) =>
            {
                workerLease.Key.StartLeasing();

                _output.WriteLine($"Starting [{workerLease.Key.InstanceId}]");
            });

            _leaseAllocatedEvent.WaitOne(TimeSpan.FromMinutes(1));
            _leaseExpiredEvent.WaitOne(TimeSpan.FromMinutes(2));
            await Task.Delay(TimeSpan.FromMilliseconds(1000));

            foreach (var workerLease in _workerLeases)
            {
                workerLease.Key.StopLeasing();

                _output.WriteLine($"Stopping [{workerLease.Key.InstanceId}]");
            }

            // Assert
            // assert that the lease is assigned to the same instances 
            using (new AssertionScope())
            {
                _allocatedList.Should().HaveCount(3);
                _allocatedList.Select(lease => lease.Key.InstanceId).Distinct().Should().HaveCount(1);
                // priority of the workerProcesses that got the lease should be 0
                _allocatedList.Select(lease => lease.Value).Should().AllBeEquivalentTo(0);
                _expiredList.Should().HaveCount(3);
            }
        }

        [Fact, IsIntegration]
        public async Task WorkerLease_MultipleWorkersPerInstanceIdStaggeredStart_LeaseAllocatedToAllWorkersWithSameInstanceId()
        {
            // Arrange
            var leaseStore = _serviceProvider.GetService<ILeaseStore>();

            var priorities = new Dictionary<Guid, int>
            {
                [new Guid("EA8E0D8A-274A-4E7F-B63B-471C45EA7ECE")] = 0,
                [new Guid("B4394200-D577-4EBE-85B3-2E2378692174")] = 1,
                [new Guid("4BB66678-EB4F-47C1-9211-47F740F8644B")] = 2
            };


            Parallel.ForEach(priorities, (i) =>
            {
                var priority = i;
                // setup 3 workers for each priority
                SetupWorkerLeases(3, _ => priority.Value, "WorkerLease_MultipleWorkersPerInstanceIdStaggeredStart_LeaseAllocatedToAllWorkersWithSameInstanceId", priority.Key);
            });

            // Act
            await leaseStore.InitialiseAsync();

            void StartFunc(KeyValuePair<IWorkerLease, int> workerLease)
            {
                workerLease.Key.StartLeasing();
                _output.WriteLine($"[{DateTime.UtcNow}] Starting [{workerLease.Key.InstanceId}, priority {workerLease.Value}]");
            }

            // start worker processes priority 1 and 2 first
            Parallel.ForEach(_workerLeases.Where(kv => kv.Value != 0), StartFunc);

            _leaseAllocatedEvent.WaitOne(TimeSpan.FromMinutes(1));

            // wait and start worker processes with priority 0
            Parallel.ForEach(_workerLeases.Where(kv => kv.Value == 0), StartFunc);
            
            _leaseExpiredEvent.WaitOne(TimeSpan.FromSeconds(30));
            _leaseAllocatedEvent.WaitOne(TimeSpan.FromMinutes(1));
            _leaseExpiredEvent.WaitOne(TimeSpan.FromSeconds(30));
             
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            foreach (var (workerLease, _) in _workerLeases)
            {
                workerLease.StopLeasing();
                _output.WriteLine($"[{DateTime.UtcNow}] Stopping [{workerLease.InstanceId}]");
            }

            // Assert
            using (new AssertionScope())
            {
                _allocatedList.Should().HaveCount(6);
                _allocatedList.Select(lease => lease.Key.InstanceId).Distinct().Should().HaveCount(2);
                _allocatedList.Select(lease => lease.Value).Should().BeEquivalentTo(new List<int> { 1, 1, 1, 0, 0, 0 });
                _expiredList.Should().HaveCount(6);
            }
        }

        private void SetupWorkerLeases(int leaseCount, Func<int, int> assignPriorityFunc, string workerType = "workertype", Guid? instanceId = null)
        {
            for (int i = 0; i < leaseCount; i++)
            {
                var options = Options.Create(new WorkerLeaseOptions
                {
                    LeaseInterval = new TimeSpan(0, 0, 30),
                    Priority = assignPriorityFunc(i),
                    InstanceId = instanceId,
                    WorkerType = workerType
                });

                var workerLease = CreateWorkerLease(options);



                workerLease.LeaseAllocated += (sender, args) =>
                {
                    _output.WriteLine($"[{DateTime.UtcNow}] Lease allocated to [{workerLease.InstanceId}] Expiry: [{args.Expiry}]");
                    _allocatedList.TryAdd((IWorkerLease)sender, options.Value.Priority);

                    _leaseAllocatedEvent.Set();
                    _leaseAllocatedEvent.Reset();
                };

                workerLease.LeaseExpired += (sender, args) =>
                {
                    _output.WriteLine($"[{DateTime.UtcNow}] Lease expired for [{workerLease.InstanceId}]");
                    _expiredList.Add((IWorkerLease)sender);

                    _leaseExpiredEvent.Set();
                    _leaseExpiredEvent.Reset();
                };

                _workerLeases.TryAdd(workerLease, options.Value.Priority);
            }
        }

        private WorkerLease CreateWorkerLease(IOptions<WorkerLeaseOptions> options)
        {
            var telemetry = _serviceProvider.GetService<IBigBrother>();
            var leaseStore = _serviceProvider.GetService<ILeaseStore>();
            var slottedInterval = _serviceProvider.GetService<ISlottedInterval>();
            var timer = _serviceProvider.GetService<ITimer>();
            var leaseAllocator = new LeaseAllocator(telemetry, leaseStore, slottedInterval, options);

            var workerLease = new WorkerLease(telemetry, leaseAllocator, timer, slottedInterval, options);
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