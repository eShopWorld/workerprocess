using System;
using System.Threading;
using Eshopworld.Tests.Core;
using System.Threading.Tasks;
using EShopworld.WorkerProcess.Infrastructure;
using FluentAssertions;
using Xunit;
using System.Runtime.CompilerServices;

namespace EShopworld.WorkerProcess.UnitTests.Infrastructure
{
    public class SystemTimerTests
    {
        
        [Fact, IsUnit]
        public void SystemTimer_WhenLoopStarts_ExecutorShouldRun()
        {
            //Arrange
            var timer = new SystemTimer();
            var manualReset=new ManualResetEvent(false);

            //Act
            var isCalled = false;
            var task=timer.ExecutePeriodicallyIn(TimeSpan.FromMilliseconds(1), async (token)=>
            {
                isCalled = true;
                manualReset.Set();
                return await Task.FromResult(TimeSpan.Zero);
            });

            manualReset.WaitOne(TimeSpan.FromMilliseconds(200));
            timer.Stop();

            //Assert
            Func<Task> act = async () => await task;
            act.Should().Throw<OperationCanceledException>();
            isCalled.Should().BeTrue();
        }

        [Fact, IsUnit]
        public async Task ExecutePeriodicallyIn_WhenTaskCancelled_LeasingIsCancelled()
        {
            // Arrange

            var cts = new CancellationTokenSource();
            cts.CancelAfter(1000);
            var timer = new SystemTimer();
            var isCalled = false;

            // Act
            var task = timer.ExecutePeriodicallyIn(TimeSpan.FromMilliseconds(500), async
                (token) => 
            {
                isCalled = true;
                   return await TestHandler(TestOperation, token);
                }, cts.Token).ConfigureAwait(false);

            Func<Task> act = async () => await task;

            // Assert
            act.Should().Throw<OperationCanceledException>();
            isCalled.Should().BeTrue();
        }

        [Fact, IsUnit]
        public void SystemTimer_WhenTimerStops_ExecutorShouldNotRun()
        {
            //Arrange
            var timer = new SystemTimer();


            //Act
            var isCalled = false; 
            var task= timer.ExecutePeriodicallyIn(TimeSpan.FromMilliseconds(500), (token) =>
            {
                isCalled = true;
                return Task.FromResult(TimeSpan.Zero);
            });

            timer.Stop();

            //Assert
            Func<Task> act = async() => await task;
            act.Should().Throw<OperationCanceledException>();
            isCalled.Should().BeFalse();

        }

        private async Task<TimeSpan> TestHandler(Func<CancellationToken, Task<TimeSpan>> operation, CancellationToken token)
        {
            try
            {
                var result = await operation(token).ConfigureAwait(false);

                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }

        private async Task<TimeSpan> TestOperation(CancellationToken token)
        {
            return await Task.FromResult(TimeSpan.Zero);
        }
    }
}
