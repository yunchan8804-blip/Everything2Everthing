using System;
using System.Collections.Generic;
using System.Linq;
using Everything2Everything.Core.Ads;
using Xunit;

namespace Everything2Everything.Tests;

public class AdServiceTests
{
    [Fact]
    public void GetBannerAds_ReturnsDefaultHouseAds_NotEmpty()
    {
        var service = new AdService();
        var bannerAds = service.GetBannerAds();

        Assert.NotNull(bannerAds);
        Assert.NotEmpty(bannerAds);
        Assert.All(bannerAds, ad =>
        {
            Assert.Equal(AdType.Banner, ad.Type);
            Assert.False(string.IsNullOrWhiteSpace(ad.Title));
            Assert.False(string.IsNullOrWhiteSpace(ad.Description));
            Assert.False(string.IsNullOrWhiteSpace(ad.TargetUrl));
        });
    }

    [Fact]
    public void GetLargeCardAds_ReturnsDefaultLargeCardAds_NotEmpty()
    {
        var service = new AdService();
        var largeAds = service.GetLargeCardAds();

        Assert.NotNull(largeAds);
        Assert.NotEmpty(largeAds);
        Assert.All(largeAds, ad =>
        {
            Assert.Equal(AdType.LargeCard, ad.Type);
            Assert.False(string.IsNullOrWhiteSpace(ad.Title));
            Assert.False(string.IsNullOrWhiteSpace(ad.Description));
            Assert.False(string.IsNullOrWhiteSpace(ad.TargetUrl));
        });
    }

    [Fact]
    public void GetNextBannerAd_RotatesThroughAds()
    {
        var service = new AdService();
        var banners = service.GetBannerAds();

        if (banners.Count > 1)
        {
            var first = service.GetNextBannerAd();
            var second = service.GetNextBannerAd();

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.NotEqual(first.Id, second.Id);
        }
        else
        {
            var ad = service.GetNextBannerAd();
            Assert.NotNull(ad);
        }
    }

    [Fact]
    public void OpenAdUrl_InvalidOrEmptyUrl_ReturnsFalse()
    {
        var service = new AdService();

        Assert.False(service.OpenAdUrl(new AdItem { TargetUrl = "" }));
        Assert.False(service.OpenAdUrl(new AdItem { TargetUrl = "javascript:alert(1)" }));
        Assert.False(service.OpenAdUrl(new AdItem { TargetUrl = "not-a-valid-url" }));
    }

    [Fact]
    public void LoadAdsFromJson_ValidJson_PopulatesCustomAds()
    {
        var service = new AdService();
        string json = @"[
            {
                ""Id"": ""test-banner-1"",
                ""Title"": ""커스텀 파트너스 배너"",
                ""Description"": ""안전하고 빠른 클라우드 스토리지"",
                ""BadgeText"": ""스폰서"",
                ""ImageUrl"": ""https://example.com/banner.png"",
                ""TargetUrl"": ""https://example.com/promo"",
                ""CtaText"": ""지금 확인"",
                ""Type"": 0,
                ""IsActive"": true
            },
            {
                ""Id"": ""test-large-1"",
                ""Title"": ""대용량 미디어 솔루션"",
                ""Description"": ""초고속 무손실 변환을 비즈니스에 도입하세요"",
                ""BadgeText"": ""AD"",
                ""ImageUrl"": ""https://example.com/large.png"",
                ""TargetUrl"": ""https://example.com/b2b"",
                ""CtaText"": ""솔루션 보기"",
                ""Type"": 1,
                ""IsActive"": true
            }
        ]";

        int count = service.LoadAdsFromJson(json);

        Assert.Equal(2, count);
        var banners = service.GetBannerAds();
        var largeCards = service.GetLargeCardAds();

        Assert.Contains(banners, b => b.Id == "test-banner-1");
        Assert.Contains(largeCards, l => l.Id == "test-large-1");
    }
}
