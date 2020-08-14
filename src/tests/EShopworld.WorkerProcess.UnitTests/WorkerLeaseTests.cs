using System;
using System.Threading.Tasks;
using System.Timers;
using Eshopworld.Core;
using Eshopworld.Tests.Core;
using EShopworld.WorkerProcess.Configuration;
using EShopworld.WorkerProcess.Infrastructure;
using EShopworld.WorkerProcess.Stores;
using EShopworld.WorkerProcess.Telemetry;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EShopworld.WorkerProcess.UnitTests
{
    public class WorkerLeaseTests
    {
        private readonly Mock<ILeaseAllocator> _mockLeaseAllocator;
        private readonly Mock<IBigBrother> _mockTelemetry;
        private readonly WorkerLeaseOptions _options;
        private readonly WorkerLease _workerLease;
        private readonly Mock<ITimer> _mockTimer;

        public WorkerLeaseTests()
        {
            _mockLeaseAllocator = new Mock<ILeaseAllocator>();
            _mockTelemetry = new Mock<IBigBrother>();
            _mockTimer = new Mock<ITimer>();

            _options = new WorkerLeaseOptions
            {
                LeaseInterval = new TimeSpan(0, 2, 0),
                Priority = 1,
                WorkerType = "workertype"
            };

            _workerLease = new WorkerLease(_mockTelemetry.Object, _mockLeaseAllocator.Object, _mockTimer.Object,
                Options.Create(_options));
        }
        
        [Fact, IsUnit]
        public async Task TestLeaseAsyncLeaseExpiredNoLeaseAllocated()
        {
            var eventFired = false;

            // Arrange
            ServerDateTime.UtcNowFunc = () => new DateTime(2000, 1, 1, 12, 0, 0);

            _workerLease.CurrentLease = new TestLease
            {
                InstanceId = _workerLease.InstanceId,
                LeasedUntil = new DateTime(2000, 1, 1, 11, 0, 0)
            };

            _workerLease.LeaseExpired += (sender, args) => { eventFired = true; };

            // Act
            await _workerLease.LeaseAsync();

            // Assert
            eventFired.Should().BeTrue();
            _mockTimer.VerifySet(m => m.Interval = _options.LeaseInterval.Minutes * 60 * 1000);
            _mockLeaseAllocator.Verify(m => m.ReleaseLeaseAsync(It.IsAny<ILease>()));
        }

        [Fact, IsUnit]
        public async Task TestLeaseAsyncLeaseAllocated()
        {
            var eventFired = false;

            // Arrange
            _mockLeaseAllocator.Setup(m => m.AllocateLeaseAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new TestLease {LeasedUntil = DateTime.UtcNow});

            _workerLease.LeaseAllocated += (sender, args) => { eventFired = true; };

            // Act
            await _workerLease.LeaseAsync();

            // Assert
            eventFired.Should().BeTrue();
        }

        [Fact, IsUnit]
        public void TestStartLease()
        {
            // Arrange
            ServerDateTime.UtcNowFunc = () => new DateTime(2000, 1, 1, 12, 0, 0);

            // Act
            _workerLease.StartLeasing();

            // Assert
            _mockTimer.VerifySet(m => m.Interval = _options.LeaseInterval.Minutes * 60 * 1000);
            _mockTimer.Verify(m => m.Start(), Times.Once);
        }

        [Fact, IsUnit]
        public void TestStopLease()
        {
            // Arrange
            _mockTimer.SetupRemove(m => m.Elapsed -= It.IsAny<EventHandler<ElapsedEventArgs>>());

            // Act
            _workerLease.StopLeasing();

            // Assert
            _mockTimer.Verify(m => m.Stop(), Times.Once);
            _mockTimer.VerifyRemove(m => m.Elapsed -= It.IsAny<EventHandler<ElapsedEventArgs>>());
        }

        [Fact, IsUnit]
        public async Task TestLeaseExpiredNotFiredWorkaround()
        {
            // Arrange
            double leaseIntervalMilliseconds = 0;

            // Timer Elapsed firing at expected time. If a few milliseconds after leaseIntervalMilliseconds after then leaseIntervalMilliseconds is also adjusted as expected
            ServerDateTime.UtcNowFunc = () => new DateTime(2000, 1, 1, 12, 12, 00);

            // Problem seems to stem from lease until being before timer elapsed. From AI logs, appears to be 1 second earlier
            _mockLeaseAllocator.Setup(m => m.AllocateLeaseAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new TestLease { LeasedUntil = new DateTime(2000, 1, 1, 12, 11, 59) });

            _mockTimer.SetupSet(p => p.Interval = It.IsAny<double>()).Callback<double>(value => leaseIntervalMilliseconds = value);

            var options = new WorkerLeaseOptions
            {
                LeaseInterval = new TimeSpan(0, 12, 0),
                Priority = 0,
                WorkerType = "workertype"
            };

            var workerLease = new WorkerLease(_mockTelemetry.Object, _mockLeaseAllocator.Object, _mockTimer.Object,
                Options.Create(options));

            // Act
            await workerLease.LeaseAsync();

            // Assert

            // Due to workaround, AI IntervalDelay event log shows that 5sec is ensure next interval is calculated around 12:24
            _mockTelemetry.Verify(t => t.Publish(It.Is<OperationTelemetryEvent>(e => e.OperationName == "IntervalDelay"),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>()));

            leaseIntervalMilliseconds.Should().Be(720000);

            // Without workaround, this is 1000ms.
            // TimerElapsed will fire in 1 second and we'll see a "Lease activation failed lease already acquired" OperationTelemetryEvent
            // The lease failure will then clear a perfectly valid lease from the active worker
            // At next lease interval LeaseExpired() will not fire as lease is null
        }
    }
}