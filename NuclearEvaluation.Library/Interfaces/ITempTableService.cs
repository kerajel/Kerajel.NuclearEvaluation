
namespace NuclearEvaluation.Library.Interfaces;

public interface ITempTableService : IDisposable
{
    Task BulkCopyInto<T>(string tableName, IEnumerable<T> entries) where T : class;
    Task<Guid> GetOrAdd(string? tableName = default);
    Task<IQueryable<T>?> Get<T>(string tableName) where T : class;
}