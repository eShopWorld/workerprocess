using System;
using Eshopworld.Tests.Core;
using EShopworld.WorkerProcess.Infrastructure;
using FluentAssertions;
using Xunit;

namespace EShopworld.WorkerProcess.UnitTests
{
    public class SlottedIntervalTests
    {
        private readonly SlottedInterval _slottedInterval;
        private static float _precision = 0.01F;

        public SlottedIntervalTests()
        {
            _slottedInterval = new SlottedInterval();
        }

        [Theory, IsUnit]
        [InlineData(637333444195566768L, 60000, 443.3231999996642)]     // 1 min
        [InlineData(637333444195566768L, 300000, 180443.3232000065)]    // 5 min
        [InlineData(637333444195566768L, 900000, 780443.3232000001)]    // 15 min
        [InlineData(637333444195566768L, 3600000, 780443.3232)]         // 1 hour
        [InlineData(630823248000000000L, 60000, 60000)]                 // 1 min
        [InlineData(630823248000000000L, 300000, 300000)]               // 5 min
        [InlineData(630823248000000000L, 900000, 900000)]               // 15 min
        [InlineData(630823248000000000L, 3600000, 3600000)]             // 1 hour
        public void TestInterval(long ticks, double interval, double expectedSlottedInterval)
        {
            // Arrange
            var now = new DateTime(ticks, DateTimeKind.Utc);

            // Act
            var result = _slottedInterval.Calculate(now, TimeSpan.FromMilliseconds(interval));

            // Assert
            result.TotalMilliseconds.Should().BeApproximately(expectedSlottedInterval, _precision);
        }
    }
}
