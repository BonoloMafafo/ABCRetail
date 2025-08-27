using Azure;
using Azure.Data.Tables;

namespace ABCRetail.Models
{
    public class Product : ITableEntity
    {
        public string PartitionKey { get; set; } = "PRODUCT";
        public string RowKey { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get; set; } = default!;
        public string StockKeepingUnit { get; set; } = default!;
        public double Price { get; set; }
        public string? ImageName { get; set; } 

        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
