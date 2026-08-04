namespace MyDocuMgm.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";
    public string RootPath { get; set; } = string.Empty;
    public long MaxImageBytes { get; set; } = 20 * 1024 * 1024;
    public int MaxPixelWidth { get; set; } = 8192;
    public int MaxPixelHeight { get; set; } = 8192;
    public long MaxTotalPixels { get; set; } = 40_000_000;
    public int ThumbnailMaxPixels { get; set; } = 480;
}
