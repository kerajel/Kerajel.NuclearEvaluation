using System.Linq.Expressions;

namespace NuclearEvaluation.Library.Interfaces;

public interface ITempTableService : IDisposable
{
    Task BulkCopyInto<T>(string tableName, IEnumerable<T> entries) where T : class;
    Task<IQueryable<T>> EnsureCreated<T>(string tableName) where T : class; 
    Task EnsureIndex<T, K>(string tableName, Expression<Func<T, K>> propertyExpression) where T : class;
    IQueryable<T> Get<T>(string tableName) where T : class;
    Task<K> InsertWithIdentity<T, K>(string tableName, T entry) where T : class;
}