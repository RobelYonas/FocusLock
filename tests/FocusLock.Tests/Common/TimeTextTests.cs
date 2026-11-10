using FocusLock.Core.Common;

namespace FocusLock.Tests.Common;

public class TimeTextTests
{
    [Theory]
    [InlineData(25 * 60, "25:00")]
    [InlineData(24 * 60 + 59.2, "25:00")]
    [InlineData(24 * 60 + 58.0, "24:58")]
    [InlineData(65 * 60, "1:05:00")]
    [InlineData(0, "00:00")]
    [InlineData(-5, "00:00")]
    public void Countdown_rounds_up_and_adds_hours_when_needed(double seconds, string expected)
    {
        Assert.Equal(expected, TimeText.Countdown(TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(50, "50 min")]
    [InlineData(90, "1 h 30 min")]
    [InlineData(120, "2 h")]
    [InlineData(0.3, "less than a minute")]
    public void Duration_is_short_and_plain(double minutes, string expected)
    {
        Assert.Equal(expected, TimeText.Duration(TimeSpan.FromMinutes(minutes)));
    }

    [Theory]
    [InlineData(45, "45 seconds")]
    [InlineData(60, "1 minute")]
    [InlineData(50 * 60, "50 minutes")]
    [InlineData(90 * 60, "1 hour 30 minutes")]
    [InlineData(2 * 3600, "2 hours")]
    [InlineData(3600 + 60, "1 hour 1 minute")]
    public void Spoken_spells_units_out(double seconds, string expected)
    {
        Assert.Equal(expected, TimeText.Spoken(TimeSpan.FromSeconds(seconds)));
    }
}
