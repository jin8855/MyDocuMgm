namespace MyDocuMgm.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";
    public string RootPath { get; set; } = string.Empty;
    public long MaxImageBytes { get; set; } = 20 * 1024 * 1024;
}
