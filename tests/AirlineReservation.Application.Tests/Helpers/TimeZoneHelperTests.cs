using AirlineReservation.Application.Common.Helpers;
using Xunit;

namespace AirlineReservation.Application.Tests.Helpers;

public class TimeZoneHelperTests
{
    [Theory]
    [InlineData("Asia/Baku")]
    [InlineData("UTC")]
    public void GetRequiredTimeZone_WhenValidZone_ReturnsTimeZoneInfo(string timeZoneId)
    {
        var tz = TimeZoneHelper.GetRequiredTimeZone(timeZoneId);

        Assert.NotNull(tz);
    }

    [Fact]
    public void GetRequiredTimeZone_WhenInvalidZone_ThrowsInvalidOperationExceptionFailFast()
    {
        string invalidZone = "Asia/Bakuu";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            TimeZoneHelper.GetRequiredTimeZone(invalidZone));

        Assert.Contains("Asia/Bakuu", ex.Message);
        Assert.Contains("could not be resolved", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetRequiredTimeZone_WhenNullOrWhitespace_ThrowsInvalidOperationException(string? invalidZone)
    {
        Assert.Throws<InvalidOperationException>(() =>
            TimeZoneHelper.GetRequiredTimeZone(invalidZone!));
    }
}
