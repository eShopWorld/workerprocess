using System;
using System.Collections.Generic;
using Eshopworld.Tests.Core;
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
        [MemberData(nameof(Data))]
        public void TestInterval(DateTime now, TimeSpan interval, double expectedSlottedInterval)
        {
            // Arrange

            // Act
            var result = _slottedInterval.Calculate(now, interval);

            // Assert
            result.TotalMilliseconds.Should().BeApproximately(expectedSlottedInterval, _precision);
        }

        private static DateTime OnTheHour = new DateTime(2000, 1, 1, 12, 0, 0);
        private static DateTime NotOnTheHour = new DateTime(2000, 1, 1, 11, 20, 10, 255);
        private static TimeSpan Interval_1Min = TimeSpan.FromMinutes(1);
        private static TimeSpan Interval_5Min = TimeSpan.FromMinutes(5);
        private static TimeSpan Interval_12Min = TimeSpan.FromMinutes(12);
        private static TimeSpan Interval_1Hour = TimeSpan.FromHours(1);

        public static IEnumerable<object[]> Data =>
            new List<object[]>
            {
                new object[] { NotOnTheHour, Interval_1Min, 49744.99999999807 },
                new object[] { NotOnTheHour, Interval_5Min, 289744.9999999964 },
                new object[] { NotOnTheHour, Interval_12Min, 229744.99999999808 },
                new object[] { NotOnTheHour, Interval_1Hour, 2389745.000000001 },
                new object[] { OnTheHour, Interval_1Min, Interval_1Min.TotalMilliseconds },
                new object[] { OnTheHour, Interval_5Min, Interval_5Min.TotalMilliseconds },
                new object[] { OnTheHour, Interval_12Min, Interval_12Min.TotalMilliseconds },
                new object[] { OnTheHour, Interval_1Hour, Interval_1Hour.TotalMilliseconds },
            };
    }
}
