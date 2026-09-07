namespace Everything2Everything.Core.Ads;

public enum AdType
{
    Banner,
    LargeCard
}

public class AdItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string BadgeText { get; set; } = "AD";
    public string ImageUrl { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public string CtaText { get; set; } = "자세히 보기";
    public AdType Type { get; set; } = AdType.Banner;
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; } = 0;
}
