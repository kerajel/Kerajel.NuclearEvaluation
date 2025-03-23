using LinqToDB;
using LinqToDB.Data;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Extensions;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.Temporary;
using System.Linq.Expressions;

namespace NuclearEvaluation.Shared.Services;

public class TempTableService : ITempTableService
{
    const TableOptions tableOptions = TableOptions.IsGlobalTemporaryStructure;
    const int maxBatchSize = 10_000;
    const int bulkCopyTimeout = 60 * 5;

    readonly Dictionary<string, dynamic> tables = [];

    readonly TempTableServiceSettings _settings;
    readonly IDbContextFactory<NuclearEvaluationServerDbContext> _dbContextFactory;
    readonly NuclearEvaluationServerDbContext _dbContext;
    readonly DataConnection _dataConnection;

    public TempTableService(
        IDbContextFactory<NuclearEvaluationServerDbContext> dbContextFactory,
        IOptions<TempTableServiceSettings> serviceOptions)
    {
        _settings = serviceOptions.Value;
        _dbContextFactory = dbContextFactory;
        _dbContext = dbContextFactory.CreateDbContext();
        _dataConnection = _dbContext.CreateLinqToDBConnectionDetached();
    }

    public async Task<IQueryable<T>> EnsureCreated<T>(string tableName) where T : class
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            tableName = Guid.NewGuid().ToString();
        }
        ITable<T> table = await GetOrAddInternal<T>(tableName);
        return table.AsQueryable();
    }

    public IQueryable<T>? Get<T>(string tableName) where T : class
    {
        string formattedTableName = GetFormattedTableName(tableName);

        if (tables.TryGetValue(formattedTableName, out dynamic? value) && value is ITable<T> table)
        {
            return table;
        }
        return default;
    }

    public async Task BulkCopyInto<T>(string tableName, IAsyncEnumerable<T> entries, CancellationToken ct = default) where T : class
    {
        BulkCopyOptions options = new()
        {
            TableName = GetFormattedTableName(tableName),
            TableOptions = TableOptions.IsGlobalTemporaryStructure,
            BulkCopyTimeout = bulkCopyTimeout,
            MaxBatchSize = maxBatchSize,
        };
        await _dataConnection.BulkCopyAsync(options, entries, cancellationToken: ct);
    }

    public async Task InsertWithoutIdentity<T>(string tableName, T entry, CancellationToken ct = default) where T : class
    {
        _ = await _dataConnection.InsertAsync(entry, GetFormattedTableName(tableName), token: ct);
    }

    public async Task EnsureIndex<T, K>(string tableName, Expression<Func<T, K>> propertyExpression) where T : class
    {
        var @params = new
        {
            tableName = GetFormattedTableName(tableName),
            fieldName = propertyExpression.GetPropertyName(),
        };

        await _dataConnection.ExecuteProcAsync("[DBO].EnsureIndexOnTempTableField", @params);
    }

    private async Task<ITable<T>> GetOrAddInternal<T>(string tableName) where T : class
    {
        string formattedTableName = GetFormattedTableName(tableName);

        if (tables.TryGetValue(formattedTableName, out dynamic? value))
        {
            return value is ITable<T> tbl
                ? tbl
                : throw new Exception($"Type mismatch for temporary table '{formattedTableName}'");
        }

        ITable<T> table = await _dataConnection.CreateTableAsync<T>(
            tableName: formattedTableName,
            tableOptions: tableOptions);
        tables.Add(formattedTableName, table);
        return table;
    }

    private static string GetFormattedTableName(string tableName)
    {
        return $"##{tableName.TrimStart('#')}";
    }

    public async ValueTask DisposeAsync()
    {
        //TODO remove RetainTables
        if (tables.Count != 0 && !_settings.RetainTables)
        {
            foreach (string tableName in tables.Keys)
            {
                await _dataConnection.DropTableAsync<object>(tableName: GetFormattedTableName(tableName), tableOptions: tableOptions);
                tables.Remove(tableName);
            }
        }
        _dbContext.Dispose();
        _dataConnection.Dispose();
        GC.SuppressFinalize(this);
    }
}