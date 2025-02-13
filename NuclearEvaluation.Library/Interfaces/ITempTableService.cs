
namespace NuclearEvaluation.Library.Interfaces;

public interface ITempTableService
{
    Task BulkCopyInto<T>(string tableName, IEnumerable<T> entries) where T : class;
    Task<string> CreateTempTable();
}