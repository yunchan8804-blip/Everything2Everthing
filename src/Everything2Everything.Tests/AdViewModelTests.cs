using System.Collections.Generic;
using Everything2Everything.App.ViewModels;
using Everything2Everything.Core.Ads;
using Xunit;

namespace Everything2Everything.Tests;

public class AdViewModelTests
{
    private class FakeAdService : IAdService
    {
        public List<AdItem> Banners { get; } = new()
        {
            new AdItem { Id = "b1", Title = "배너 1", TargetUrl = "https://example.com/b1", Type = AdType.Banner },
            new AdItem { Id = "b2", Title = "배너 2", TargetUrl = "https://example.com/b2", Type = AdType.Banner }
        };

        public List<AdItem> Cards { get; } = new()
        {
            new AdItem { Id = "c1", Title = "카드 1", TargetUrl = "https://example.com/c1", Type = AdType.LargeCard },
            new AdItem { Id = "c2", Title = "카드 2", TargetUrl = "https://example.com/c2", Type = AdType.LargeCard }
        };

        public AdItem? LastOpenedAd { get; private set; }
        private int _bIndex = 0;
        private int _cIndex = 0;

        public IReadOnlyList<AdItem> GetBannerAds() => Banners;
        public IReadOnlyList<AdItem> GetLargeCardAds() => Cards;

        public AdItem? GetNextBannerAd()
        {
            var ad = Banners[_bIndex % Banners.Count];
            _bIndex++;
            return ad;
        }

        public AdItem? GetNextLargeCardAd()
        {
            var ad = Cards[_cIndex % Cards.Count];
            _cIndex++;
            return ad;
        }

        public System.Threading.Tasks.Task<int> RefreshAdsFromFeedAsync(string feedUrl, System.Threading.CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.FromResult(0);
        public int LoadAdsFromJson(string json) => 0;

        public bool OpenAdUrl(AdItem ad)
        {
            LastOpenedAd = ad;
            return true;
        }
    }

    [Fact]
    public void Constructor_InitializesAdsAndVisibility()
    {
        var fake = new FakeAdService();
        var vm = new AdViewModel(fake);

        Assert.NotNull(vm.CurrentBannerAd);
        Assert.NotNull(vm.CurrentLargeAd);
        Assert.True(vm.IsBannerVisible);
        Assert.True(vm.IsLargeCardVisible);
    }

    [Fact]
    public void ClickBannerAdCommand_OpensAdUrl()
    {
        var fake = new FakeAdService();
        var vm = new AdViewModel(fake);

        vm.ClickBannerAdCommand.Execute(null);

        Assert.NotNull(fake.LastOpenedAd);
        Assert.Equal(vm.CurrentBannerAd?.Id, fake.LastOpenedAd.Id);
    }

    [Fact]
    public void DismissBannerCommand_HidesBanner()
    {
        var fake = new FakeAdService();
        var vm = new AdViewModel(fake);

        Assert.True(vm.IsBannerVisible);
        vm.DismissBannerCommand.Execute(null);
        Assert.False(vm.IsBannerVisible);
    }

    [Fact]
    public void RotateBannerAd_ChangesCurrentBanner()
    {
        var fake = new FakeAdService();
        var vm = new AdViewModel(fake);

        var firstId = vm.CurrentBannerAd?.Id;
        vm.RotateBannerAd();
        var secondId = vm.CurrentBannerAd?.Id;

        Assert.NotEqual(firstId, secondId);
    }
}
