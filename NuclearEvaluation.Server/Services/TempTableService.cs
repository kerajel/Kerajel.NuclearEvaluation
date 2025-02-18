using LinqToDB;
using LinqToDB.Data;
using LinqToDB.EntityFrameworkCore;
using NuclearEvaluation.Library.Interfaces;
using NuclearEvaluation.Library.Models.DataManagement;
using NuclearEvaluation.Server.Data;

namespace NuclearEvaluation.Server.Services;

public class TempTableService : DbServiceBase, ITempTableService
{
    const TableOptions tableOptions = TableOptions.IsGlobalTemporaryStructure;

    readonly Dictionary<string, dynamic> tables = [];

    public TempTableService(NuclearEvaluationServerDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<string> GetOrAdd(string? tableName = default)
    {
        tableName = tableName ?? Guid.NewGuid().ToString();
        _ = await GetOrAdd<StemPreviewEntry>(tableName);
        return tableName;
    }

    public async Task<IQueryable<T>?> Get<T>(string tableName) where T : class
    {
        string formattedTableName = GetTableName(tableName);

        if (tables.TryGetValue(formattedTableName, out dynamic? value) && value is ITable<T> table)
        {
            return table;
        }
        return default;
    }

    public async Task BulkCopyInto<T>(string tableName, IEnumerable<T> entries) where T : class
    {
        BulkCopyOptions options = new()
        {
            TableName = GetTableName(tableName),
            TableOptions = TableOptions.IsGlobalTemporaryStructure,
        };
        using DataConnection dataConnection = _dbContext.CreateLinqToDBConnection();
        await dataConnection.BulkCopyAsync(options, entries);
    }

    private async Task<ITable<T>> GetOrAdd<T>(string tableName) where T : class
    {
        string formattedTableName = GetTableName(tableName);

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

    private static string GetTableName(string tableName)
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
                dc.DropTable<object>(tableName: GetTableName(tableName), tableOptions: tableOptions);
                tables.Remove(tableName);
            }
        }
    }
}