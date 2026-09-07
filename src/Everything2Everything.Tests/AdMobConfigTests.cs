using Everything2Everything.Core.Ads;
using Xunit;

namespace Everything2Everything.Tests;

public class AdMobConfigTests
{
    [Fact]
    public void AdMobConfig_DefaultState_IsValid()
    {
        var config = new AdMobConfig();

        Assert.NotNull(config.AppId);
        Assert.NotNull(config.BannerAdUnitId);
        Assert.NotNull(config.InterstitialAdUnitId);
    }

    [Fact]
    public void GenerateAppAdsTxt_ReturnsValidGoogleSpecification()
    {
        var config = new AdMobConfig
        {
            PublisherId = "pub-1234567890123456"
        };

        string appAdsTxt = config.GenerateAppAdsTxt();

        Assert.Contains("google.com, pub-1234567890123456, DIRECT, f08c47fec0942fa0", appAdsTxt);
    }
}
