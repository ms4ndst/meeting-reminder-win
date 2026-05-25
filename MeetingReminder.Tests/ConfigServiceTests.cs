using MeetingReminder.Core;
using MeetingReminder.Core.Models;
using Xunit;

namespace MeetingReminder.Tests;

public class ConfigServiceTests
{
    [Fact]
    public void Default_Config_Has_Expected_Values()
    {
        var cfg = AppConfig.Default();

        Assert.Equal(5, cfg.AlertMinutesBefore);
        Assert.Equal(14.0, cfg.FlightDurationSeconds);
        Assert.Equal("Mocha", cfg.Theme);
        Assert.Equal("Mauve", cfg.Accent);
        Assert.False(cfg.StartWithWindows);
        Assert.False(cfg.StartMinimized);
    }

    [Fact]
    public void Normalised_Clamps_Alert_Minutes()
    {
        var cfg = AppConfig.Default() with { AlertMinutesBefore = 100 };
        var normalised = cfg.Normalised();
        Assert.Equal(30, normalised.AlertMinutesBefore);
    }

    [Fact]
    public void Normalised_Fixes_Zero_Alert()
    {
        var cfg = AppConfig.Default() with { AlertMinutesBefore = 0 };
        var normalised = cfg.Normalised();
        Assert.Equal(5, normalised.AlertMinutesBefore); // default
    }

    [Fact]
    public void Normalised_Fixes_Empty_Theme()
    {
        var cfg = AppConfig.Default() with { Theme = "" };
        var normalised = cfg.Normalised();
        Assert.Equal("Mocha", normalised.Theme);
    }

    [Fact]
    public void Serialise_RoundTrip()
    {
        var original = AppConfig.Default();
        var json = ConfigService.Serialize(original);
        var loaded = ConfigService.Deserialize(json);

        Assert.NotNull(loaded);
        Assert.Equal(original.AlertMinutesBefore, loaded!.AlertMinutesBefore);
        Assert.Equal(original.FlightDurationSeconds, loaded.FlightDurationSeconds);
        Assert.Equal(original.Theme, loaded.Theme);
        Assert.Equal(original.Accent, loaded.Accent);
    }

    [Fact]
    public void Save_And_Load_RoundTrip()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"mr-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            var configPath = Path.Combine(tempDir, "config.json");
            var svc = new ConfigService(configPath);

            var original = AppConfig.Default() with { AlertMinutesBefore = 10, Theme = "Latte" };
            svc.Save(original);
            var loaded = svc.Load();

            Assert.Equal(10, loaded.AlertMinutesBefore);
            Assert.Equal("Latte", loaded.Theme);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}
