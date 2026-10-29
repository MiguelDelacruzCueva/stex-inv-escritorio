using Refit;
using STEX.Inv.Desktop.Core.DTOs;

namespace STEX.Inv.Desktop.Infrastructure.Api;

public interface ISyncApi
{
    [Get("/api/sync/download")]
    Task<SyncDownloadResponseDto> DownloadAsync(CancellationToken cancellationToken = default);

    [Post("/api/sync/upload")]
    Task<SyncUploadResponseDto> UploadAsync(
        [Body] SyncUploadRequestDto request,
        CancellationToken cancellationToken = default);
}
