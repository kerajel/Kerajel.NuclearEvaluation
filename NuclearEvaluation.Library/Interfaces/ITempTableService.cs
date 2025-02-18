
namespace NuclearEvaluation.Library.Interfaces;

public interface ITempTableService : IDisposable
{
    Task BulkCopyInto<T>(string tableName, IEnumerable<T> entries) where T : class;
    Task<string> EnsureCreated<T>(string? tableName = default) where T : class;
    IQueryable<T>? Get<T>(string tableName) where T : class;
    Task<K> InsertWithIdentity<T, K>(string tableName, T entry) where T : class;
}