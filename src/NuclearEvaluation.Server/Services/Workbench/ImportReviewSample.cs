using System.Security.Cryptography;
using NuclearEvaluation.Server.Services.RadNet;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Services.Workbench;

public sealed class ImportReviewSample(
    RadNetSnapshot snapshot,
    IConfiguration configuration,
    IHostEnvironment environment
)
{
    const string Member = "co_colorado_springs_2025-2008/CO_COLORADO_SPRINGS_2025.csv";
    const string CsvSha256 = "0614593A2DB2C3225CDE507B9B32F2D2A35F73DE4E6BF03C39BC4A6CAF9CE573";
    const string Station = "Colorado Springs";
    const string State = "CO";
    const int Year = 2025;

    readonly string _path = Path.GetFullPath(
        configuration["ImportReview:SamplePath"] ?? "App_Data/radnet-import-sample.csv",
        environment.ContentRootPath
    );

    internal bool IsAvailable => File.Exists(_path);

    internal async Task<ImportReviewSource?> FindSourceAsync(CancellationToken ct)
    {
        RadNetQuery query = new(
            RadNetEntity.SourceFile,
            [
                new(
                    RadNetEntity.Station,
                    false,
                    [
                        new(nameof(RadNetRow.State), RadNetOperator.Equal, State),
                        new(nameof(RadNetRow.StationName), RadNetOperator.Equal, Station),
                    ]
                ),
                new(
                    RadNetEntity.SourceFile,
                    false,
                    [new(nameof(RadNetRow.Year), RadNetOperator.Equal, "2025")]
                ),
            ],
            Top: 2
        );
        RadNetQueryResult? result = await snapshot.QueryAsync(query, ct);
        if (
            result is not { Items.Count: 1 }
            || result.Items[0]
                is not {
                    SourceFileId: int fileId,
                    FileName: Member,
                    ArchiveUrl: { } url,
                    SourceRows: long count,
                    ArchiveSha256: { } archiveSha,
                }
            || count <= 0
        )
            return null;
        return new(
            fileId,
            State,
            Station,
            Year,
            Member,
            url,
            count,
            "CO: COLORADO SPRINGS",
            archiveSha
        );
    }

    internal async Task<Stream?> OpenVerifiedAsync(CancellationToken ct)
    {
        FileInfo file = new(_path);
        if (!file.Exists || file.Length > ImportReviewCatalog.MaxUploadBytes)
            return null;
        byte[] bytes = await File.ReadAllBytesAsync(_path, ct);
        return Convert
            .ToHexString(SHA256.HashData(bytes))
            .Equals(CsvSha256, StringComparison.Ordinal)
            ? new MemoryStream(bytes, writable: false)
            : null;
    }
}
