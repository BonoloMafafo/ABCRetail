using Azure;
using Azure.Data.Tables;

namespace ABCRetail.Services
{
    public class TableStorage
    {
        private readonly TableServiceClient _serviceClient;

        public TableStorage(IConfiguration config)
        {
            var conn = config.GetConnectionString("AzureStorage");
            _serviceClient = new TableServiceClient(conn);
        }

        public async Task<TableClient> GetTableAsync(string tableName)
        {
            var table = _serviceClient.GetTableClient(tableName);
            await table.CreateIfNotExistsAsync();
            return table;
        }

        public async Task AddAsync<T>(string tableName, T entity) where T : class, ITableEntity, new()
        {
            var table = await GetTableAsync(tableName);
            await table.AddEntityAsync(entity);
        }

        public async Task<List<T>> GetAllAsync<T>(string tableName) where T : class, ITableEntity, new()
        {
            var table = await GetTableAsync(tableName);
            var list = new List<T>();
            await foreach (var e in table.QueryAsync<T>())
                list.Add(e);
            return list;
        }
    }
}
