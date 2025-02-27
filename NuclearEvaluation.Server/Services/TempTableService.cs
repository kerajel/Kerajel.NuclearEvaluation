using LinqToDB;
using LinqToDB.Data;
using LinqToDB.EntityFrameworkCore;
using NuclearEvaluation.Library.Extensions;
using NuclearEvaluation.Library.Interfaces;
using NuclearEvaluation.Server.Data;
using System.Linq.Expressions;

namespace NuclearEvaluation.Server.Services;

public class TempTableService : DbServiceBase, ITempTableService
{
    const TableOptions tableOptions = TableOptions.IsGlobalTemporaryStructure;
    const int maxBatchSize = 10_000;
    const int bulkCopyTimeout = 60;

    readonly Dictionary<string, dynamic> tables = [];

    public TempTableService(NuclearEvaluationServerDbContext dbContext) : base(dbContext)
    {
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

    public IQueryable<T> Get<T>(string tableName) where T : class
    {
        string formattedTableName = GetFormattedTableName(tableName);

        if (tables.TryGetValue(formattedTableName, out dynamic? value) && value is ITable<T> table)
        {
            return table;
        }
        throw new Exception($"Temporary table '{tableName}' was not found");
    }

    public async Task BulkCopyInto<T>(string tableName, IEnumerable<T> entries, CancellationToken ct = default) where T : class
    {
        BulkCopyOptions options = new()
        {
            TableName = GetFormattedTableName(tableName),
            TableOptions = TableOptions.IsGlobalTemporaryStructure,
            BulkCopyTimeout = bulkCopyTimeout,
            MaxBatchSize = maxBatchSize,
        };
        using DataConnection dataConnection = _dbContext.CreateLinqToDBConnection();
        await dataConnection.BulkCopyAsync(options, entries, cancellationToken: ct);
    }

    public async Task InsertWithoutIdentity<T>(string tableName, T entry, CancellationToken ct = default) where T : class
    {
        using DataConnection dataConnection = _dbContext.CreateLinqToDBConnection();
        object id = await dataConnection.InsertAsync(entry, GetFormattedTableName(tableName), token: ct);
    }

    public async Task EnsureIndex<T, K>(string tableName, Expression<Func<T, K>> propertyExpression) where T : class
    {
        var @params = new
        {
            tableName = GetFormattedTableName(tableName),
            fieldName = propertyExpression.GetPropertyName(),
        };

        using DataConnection dc = _dbContext.CreateLinqToDBConnection();
        await dc.ExecuteProcAsync("[DBO].EnsureIndexOnTempTableField", @params);
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

        DataConnection dataConnection = _dbContext.CreateLinqToDBConnection();
        ITable<T> table = await dataConnection.CreateTableAsync<T>(
            tableName: formattedTableName,
            tableOptions: tableOptions);
        tables.Add(formattedTableName, table);
        return table;
    }

    private static string GetFormattedTableName(string tableName)
    {
        return $"##{tableName.TrimStart('#')}";
    }

    public void Dispose()
    {
        if (tables.Count != 0)
        {
            using DataConnection dc = _dbContext.CreateLinqToDBConnection();
            foreach (string tableName in tables.Keys)
            {
                dc.DropTable<object>(tableName: GetFormattedTableName(tableName), tableOptions: tableOptions);
                tables.Remove(tableName);
            }
        }
        GC.SuppressFinalize(this);
    }
}