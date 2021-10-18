using System;
using System.Threading;
using System.Threading.Tasks;
using Eshopworld.Core;
using Eshopworld.Tests.Core;
using EShopworld.WorkerProcess.Configuration;
using EShopworld.WorkerProcess.Infrastructure;
using EShopworld.WorkerProcess.Stores;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EShopworld.WorkerProcess.UnitTests
{
    public class WorkerLeaseTests
    {
        private readonly Mock<ILeaseAllocator> _mockLeaseAllocator;
        private readonly Mock<ISlottedInterval> _mockSlottedInterval;
        private readonly Mock<IBigBrother> _mockTelemetry;
        private readonly WorkerLeaseOptions _options;
        private readonly WorkerLease _workerLease;
        private readonly Mock<ITimer> _mockTimer;

        public WorkerLeaseTests()
        {
            _mockLeaseAllocator = new Mock<ILeaseAllocator>();
            _mockSlottedInterval = new Mock<ISlottedInterval>();
            _mockTelemetry = new Mock<IBigBrother>();
            _mockTimer = new Mock<ITimer>();

            _options = new WorkerLeaseOptions
            {
                LeaseInterval = new TimeSpan(0, 2, 0),
                Priority = 1,
                WorkerType = "workertype"
            };

            _workerLease = new WorkerLease(
                _mockTelemetry.Object,
                _mockLeaseAllocator.Object,
                _mockTimer.Object,
                _mockSlottedInterval.Object,
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
                LeasedUntil = new DateTime(2000, 1, 1, 11, 0, 0),
                Interval = TimeSpan.FromMinutes(2)
            };

            _workerLease.LeaseExpired += (sender, args) => { eventFired = true; };

            // Act
            await _workerLease.LeaseAsync(It.IsAny<CancellationToken>());

            // Assert
            eventFired.Should().BeTrue();
            _mockSlottedInterval.Verify(m => m.Calculate(new DateTime(2000, 1, 1, 12, 0, 0), TimeSpan.FromMinutes(2)));
            _mockLeaseAllocator.Verify(m => m.ReleaseLeaseAsync(It.IsAny<ILease>()), Times.Never);
        }

        [Fact, IsUnit]
        public async Task TestLeaseAsyncLeaseAllocated()
        {
            var eventFired = false;

            // Arrange
            _mockLeaseAllocator.Setup(m => m.AllocateLeaseAsync(It.IsAny<Guid>(),It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TestLease {
                    LeasedUntil = DateTime.UtcNow,
                    Interval = TimeSpan.FromMinutes(2)
                });

            _workerLease.LeaseAllocated += (sender, args) => { eventFired = true; };

            // Act
            await _workerLease.LeaseAsync(It.IsAny<CancellationToken>());

            // Assert
            eventFired.Should().BeTrue();
        }

        [Fact, IsUnit]
        public async Task StartLeasingAsync_WhenExistingLeaseAvailable_LeaseIsAllocated()
        {
            // Arrange

            var eventFired = false;
            var instanceId = Guid.NewGuid();

            _mockLeaseAllocator.Setup(m => m.TryReacquireLease(
                    It.Is<Guid>(id => id == instanceId),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TestLease
                {
                    LeasedUntil = DateTime.UtcNow + TimeSpan.FromMinutes(5),
                    Interval = TimeSpan.FromMinutes(2),
                    InstanceId = instanceId
                });

            var workerLease = BuildWorkerLeaseWithFixedInstanceId(instanceId);

            workerLease.LeaseAllocated += (sender, args) =>
            {
                eventFired = true;
            };

            // Act
            await workerLease.StartLeasingAsync(It.IsAny<CancellationToken>()).ConfigureAwait(true);

            // Assert
            eventFired.Should().BeTrue();
        }

        [Fact, IsUnit]
        public async Task StartLeasingAsync_WhenExistingLeaseNotAvailable_LeaseIsNotAllocated()
        {
            // Arrange

            var eventFired = false;
            var instanceId = Guid.NewGuid();

            _mockLeaseAllocator.Setup(m => m.TryReacquireLease(
                    It.Is<Guid>(id => id == instanceId),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((TestLease)null);

            var workerLease = BuildWorkerLeaseWithFixedInstanceId(instanceId);

            workerLease.LeaseAllocated += (sender, args) =>
            {
                eventFired = true;
            };

            // Act
            await workerLease.StartLeasingAsync(It.IsAny<CancellationToken>()).ConfigureAwait(true);

            // Assert
            eventFired.Should().BeFalse();
        }

        [Fact, IsUnit]
        public async Task TestStartLease()
        {
            // Arrange
            ServerDateTime.UtcNowFunc = () => new DateTime(2000, 1, 1, 12, 0, 0);

            // Act
            await _workerLease.StartLeasingAsync(CancellationToken.None);
            

            // Assert
            _mockSlottedInterval.Verify(m => m.Calculate(new DateTime(2000, 1, 1, 12, 0, 0), TimeSpan.FromMinutes(2)));
            _mockTimer.Verify(m => m.ExecutePeriodicallyIn(It.IsAny<TimeSpan>(),It.IsAny<Func<CancellationToken,Task<TimeSpan>>>()), Times.Once);
        }

        [Fact, IsUnit]
        public void TestStopLease()
        {
           
            // Act
            _workerLease.StopLeasing();

            // Assert
            _mockTimer.Verify(m => m.Stop(), Times.Once);
        }

        [Fact, IsUnit]
        public void WorkerLease_WhenInstanceIdIsPresentInOptions_NewInstanceIdIsNotCreated()
        {
            var options = new WorkerLeaseOptions
            {
                LeaseInterval = new TimeSpan(0, 2, 0),
                Priority = 1,
                WorkerType = "workertype",
                InstanceId = new Guid("f167a595-5b18-4d54-ab8a-f14faafb2214")
            };
            var workerLease = new WorkerLease(
                _mockTelemetry.Object,
                _mockLeaseAllocator.Object, 
                _mockTimer.Object,
                _mockSlottedInterval.Object,
                Options.Create(options));

            workerLease.InstanceId.ToString().Should().Be("f167a595-5b18-4d54-ab8a-f14faafb2214");
        }

        [Fact, IsUnit]
        public void WorkerLease_WhenInstanceIdNotPresentInOptions_NewInstanceIdIsCreated()
        {
            var options = new WorkerLeaseOptions
            {
                LeaseInterval = new TimeSpan(0, 2, 0),
                Priority = 1,
                WorkerType = "workertype"
                
            };
            var workerLease = new WorkerLease(
                _mockTelemetry.Object,
                _mockLeaseAllocator.Object,
                _mockTimer.Object,
                _mockSlottedInterval.Object,
                Options.Create(options));

            workerLease.InstanceId.Should().NotBeEmpty();
        }

        private WorkerLease BuildWorkerLeaseWithFixedInstanceId(Guid instanceId)
        {
            var options = new WorkerLeaseOptions
            {
                LeaseInterval = new TimeSpan(0, 2, 0),
                Priority = 1,
                WorkerType = "workertype",
                InstanceId = instanceId
            };

            var workerLease = new WorkerLease(
                _mockTelemetry.Object,
                _mockLeaseAllocator.Object,
                _mockTimer.Object,
                _mockSlottedInterval.Object,
                Options.Create(options));

            return workerLease;
        }
    }
}