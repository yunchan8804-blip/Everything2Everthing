using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Everything2Everything.Core.Ads;

public class AdService : IAdService
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly List<AdItem> _ads = new();
    private readonly object _lock = new();
    private int _bannerIndex = 0;
    private int _largeCardIndex = 0;

    public AdService()
    {
        InitializeDefaultAds();
    }

    private void InitializeDefaultAds()
    {
        lock (_lock)
        {
            _ads.Clear();

            // Default House Banner Ads
            _ads.Add(new AdItem
            {
                Id = "house-banner-sponsor-1",
                Title = "초고속 무손실 미디어 솔루션",
                Description = "Everything2Everything 공식 후원 및 제휴 파트너십을 확인하세요.",
                BadgeText = "스폰서",
                ImageUrl = "pack://application:,,,/Everything2Everything;component/Assets/logo-mark.png",
                TargetUrl = "https://github.com/yunchan8804/Everything2Everthing",
                CtaText = "자세히 보기",
                Type = AdType.Banner,
                IsActive = true,
                DisplayOrder = 1
            });

            _ads.Add(new AdItem
            {
                Id = "house-banner-tip-2",
                Title = "대용량 일괄 변환 최적화",
                Description = "FFmpeg 및 LibRaw 가속 엔진으로 수천 장의 이미지를 순식간에 처리합니다.",
                BadgeText = "AD",
                ImageUrl = "pack://application:,,,/Everything2Everything;component/Assets/glyph-video.png",
                TargetUrl = "https://github.com/yunchan8804/Everything2Everthing#features",
                CtaText = "가이드 보기",
                Type = AdType.Banner,
                IsActive = true,
                DisplayOrder = 2
            });

            // Default Large Card Ads
            _ads.Add(new AdItem
            {
                Id = "house-large-card-1",
                Title = "비즈니스 미디어 변환 엔진",
                Description = "개인 및 기업을 위한 고성능 무손실 일괄 변환. 워터마크 없이 100% 무료로 자유롭게 활용하세요.",
                BadgeText = "스폰서 추천",
                ImageUrl = "pack://application:,,,/Everything2Everything;component/Assets/illus-done.png",
                TargetUrl = "https://github.com/yunchan8804/Everything2Everthing",
                CtaText = "파트너십 문의",
                Type = AdType.LargeCard,
                IsActive = true,
                DisplayOrder = 1
            });

            _ads.Add(new AdItem
            {
                Id = "house-large-card-2",
                Title = "스마트 AI 요약 & OCR 가속",
                Description = "로컬 LLM과 Tesseract OCR을 결합하여 스캔 문서와 PDF를 즉시 검색 가능한 텍스트로 변환합니다.",
                BadgeText = "AD",
                ImageUrl = "pack://application:,,,/Everything2Everything;component/Assets/glyph-ai.png",
                TargetUrl = "https://github.com/yunchan8804/Everything2Everthing",
                CtaText = "기능 살펴보기",
                Type = AdType.LargeCard,
                IsActive = true,
                DisplayOrder = 2
            });
        }
    }

    public IReadOnlyList<AdItem> GetBannerAds()
    {
        lock (_lock)
        {
            return _ads.Where(a => a.Type == AdType.Banner && a.IsActive).OrderBy(a => a.DisplayOrder).ToList();
        }
    }

    public IReadOnlyList<AdItem> GetLargeCardAds()
    {
        lock (_lock)
        {
            return _ads.Where(a => a.Type == AdType.LargeCard && a.IsActive).OrderBy(a => a.DisplayOrder).ToList();
        }
    }

    public AdItem? GetNextBannerAd()
    {
        var banners = GetBannerAds();
        if (banners.Count == 0) return null;

        lock (_lock)
        {
            var ad = banners[_bannerIndex % banners.Count];
            _bannerIndex = (_bannerIndex + 1) % banners.Count;
            return ad;
        }
    }

    public AdItem? GetNextLargeCardAd()
    {
        var cards = GetLargeCardAds();
        if (cards.Count == 0) return null;

        lock (_lock)
        {
            var ad = cards[_largeCardIndex % cards.Count];
            _largeCardIndex = (_largeCardIndex + 1) % cards.Count;
            return ad;
        }
    }

    public int LoadAdsFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var items = JsonSerializer.Deserialize<List<AdItem>>(json, options);
            if (items == null || items.Count == 0) return 0;

            lock (_lock)
            {
                foreach (var item in items)
                {
                    if (string.IsNullOrWhiteSpace(item.Id))
                    {
                        item.Id = Guid.NewGuid().ToString("N");
                    }
                    var existingIdx = _ads.FindIndex(a => a.Id == item.Id);
                    if (existingIdx >= 0)
                    {
                        _ads[existingIdx] = item;
                    }
                    else
                    {
                        _ads.Add(item);
                    }
                }
            }
            return items.Count;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<int> RefreshAdsFromFeedAsync(string feedUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(feedUrl) || !Uri.TryCreate(feedUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return 0;
        }

        try
        {
            var response = await HttpClient.GetStringAsync(uri, cancellationToken).ConfigureAwait(false);
            return LoadAdsFromJson(response);
        }
        catch
        {
            return 0;
        }
    }

    public bool OpenAdUrl(AdItem ad)
    {
        if (ad == null || string.IsNullOrWhiteSpace(ad.TargetUrl))
        {
            return false;
        }

        if (!Uri.TryCreate(ad.TargetUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            };
            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
