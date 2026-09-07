namespace Everything2Everything.Core.Ads;

/// <summary>
/// Google AdMob 공식 앱 ID, 광고 단위 ID 및 app-ads.txt 검증 구성 모델
/// </summary>
public class AdMobConfig
{
    /// <summary>Google AdMob 게시자 ID (예: pub-XXXXXXXXXXXXXXXX)</summary>
    public string PublisherId { get; set; } = string.Empty;

    /// <summary>AdMob 공식 앱 ID (형식: ca-app-pub-XXXXXXXXXXXXXXXX~YYYYYYYYYY)</summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>배너 광고 단위 ID (형식: ca-app-pub-XXXXXXXXXXXXXXXX/YYYYYYYYYY)</summary>
    public string BannerAdUnitId { get; set; } = string.Empty;

    /// <summary>전면/대형 카드 광고 단위 ID (형식: ca-app-pub-XXXXXXXXXXXXXXXX/ZZZZZZZZZZ)</summary>
    public string InterstitialAdUnitId { get; set; } = string.Empty;

    /// <summary>
    /// Google 공식 규격의 app-ads.txt 문자열 생성
    /// 형식: google.com, pub-XXXXXXXXXXXXXXXX, DIRECT, f08c47fec0942fa0
    /// </summary>
    public string GenerateAppAdsTxt()
    {
        var pubId = PublisherId.Trim();
        if (!pubId.StartsWith("pub-", System.StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(pubId))
        {
            pubId = "pub-" + pubId;
        }

        return $"google.com, {pubId}, DIRECT, f08c47fec0942fa0";
    }
}
