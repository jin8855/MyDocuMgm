using Microsoft.Extensions.Logging;
using MyDocuMgm.Application;

namespace MyDocuMgm.Infrastructure.Storage;

public sealed class MediaDiagnostics(ILogger<MediaDiagnostics> logger) : IMediaDiagnostics
{
    public void Record(string code, Guid? contentId, Guid? mediaId = null)
    {
        logger.LogWarning(
            "Media diagnostic {MediaCode} for content {ContentId} and media {MediaId}",
            code,
            contentId,
            mediaId);
    }
}
