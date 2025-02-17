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

    public async Task<string> Create(string? tableName = default)
    {
        tableName = tableName ?? $"##{Guid.NewGuid()}";
        ITable<StemPreviewEntry> table = await CreateTable<StemPreviewEntry>(tableName);
        return tableName;
    }

    public async Task<IQueryable<T>?> Get<T>(string tableName) where T : class
    {
        if (tables.TryGetValue(tableName, out dynamic? value) && value is ITable<object> table)
        {
            return table.Select(x => (T)x);
        }
        return default;
    }

    public async Task BulkCopyInto<T>(string tableName, IEnumerable<T> entries) where T : class
    {
        BulkCopyOptions options = new()
        {
            TableName = tableName,
            TableOptions = TableOptions.IsGlobalTemporaryStructure,
        };
        using DataConnection dataConnection = _dbContext.CreateLinqToDBConnection();
        await dataConnection.BulkCopyAsync(options, entries);
    }

    private async Task<ITable<T>> CreateTable<T>(string tableName) where T : class
    {
        DataConnection dataConnection = _dbContext.CreateLinqToDBConnection();
        ITable<T> table =  await dataConnection.CreateTableAsync<T>(
            tableName: tableName,
            tableOptions: tableOptions);
        tables.Add(tableName, table);
        return table;
    }

    public void Dispose()
    {
        if (tables.Count != 0)
        {
            using DataConnection dc = _dbContext.CreateLinqToDBConnection();
            foreach (string tableName in tables.Keys)
            {
                dc.DropTable<object>(tableName: tableName, tableOptions: tableOptions);
                tables.Remove(tableName);
            }
        }
    }
}