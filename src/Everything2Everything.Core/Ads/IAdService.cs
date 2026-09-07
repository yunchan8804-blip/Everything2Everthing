using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Everything2Everything.Core.Ads;

public interface IAdService
{
    IReadOnlyList<AdItem> GetBannerAds();
    IReadOnlyList<AdItem> GetLargeCardAds();
    AdItem? GetNextBannerAd();
    AdItem? GetNextLargeCardAd();
    Task<int> RefreshAdsFromFeedAsync(string feedUrl, CancellationToken cancellationToken = default);
    int LoadAdsFromJson(string json);
    bool OpenAdUrl(AdItem ad);
}
