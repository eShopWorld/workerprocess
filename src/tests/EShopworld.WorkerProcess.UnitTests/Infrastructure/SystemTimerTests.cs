using System;
using System.Threading;
using Eshopworld.Tests.Core;
using System.Threading.Tasks;
using EShopworld.WorkerProcess.Infrastructure;
using FluentAssertions;
using Xunit;

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
    }
}
