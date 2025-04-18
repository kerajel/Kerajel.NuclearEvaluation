﻿using System.Linq.Expressions;

namespace NuclearEvaluation.Server.Interfaces.Temp;

public interface ITempTableService : IAsyncDisposable
{
    Task BulkCopyInto<T>(string tableName, IAsyncEnumerable<T> entries, CancellationToken ct = default) where T : class;
    Task<IQueryable<T>> EnsureCreated<T>(string tableName) where T : class;
    Task EnsureIndex<T, K>(string tableName, Expression<Func<T, K>> propertyExpression) where T : class;
    IQueryable<T>? Get<T>(string tableName) where T : class;
    Task InsertWithoutIdentity<T>(string tableName, T entry, CancellationToken ct = default) where T : class;
}