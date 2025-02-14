
namespace NuclearEvaluation.Library.Interfaces;

public interface ITempTableService : IDisposable
{
    Task BulkCopyInto<T>(string tableName, IEnumerable<T> entries) where T : class;
    Task<string> CreateTempTable(string? tableName = default);
}