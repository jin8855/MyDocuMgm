namespace MyDocuMgm.Infrastructure.ExternalFetch;

public sealed class ExternalFetchOptions
{
    public const string SectionName = "ExternalFetch";
    public const int ApprovedConnectTimeoutSeconds = 5;
    public const int ApprovedTotalTimeoutSeconds = 15;
    public const int ApprovedMaxRedirects = 3;
    public const int ApprovedMaxRobotsRedirects = 3;
    public const int ApprovedMaxHtmlBytes = 2 * 1024 * 1024;
    public const int ApprovedMaxRobotsBytes = 512 * 1024;
    public const string RobotsProductToken = "MyDocuMgmPhase2C";
    public const string HttpUserAgent = RobotsProductToken + "/1.0";

    public int ConnectTimeoutSeconds { get; set; } = ApprovedConnectTimeoutSeconds;
    public int TotalTimeoutSeconds { get; set; } = ApprovedTotalTimeoutSeconds;
    public int MaxRedirects { get; set; } = ApprovedMaxRedirects;
    public int MaxRobotsRedirects { get; set; } = ApprovedMaxRobotsRedirects;
    public int MaxHtmlBytes { get; set; } = ApprovedMaxHtmlBytes;
    public int MaxRobotsBytes { get; set; } = ApprovedMaxRobotsBytes;
}
