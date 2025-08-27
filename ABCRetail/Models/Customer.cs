using Azure;
using Azure.Data.Tables;

namespace ABCRetail.Models
{
    public class Customer : ITableEntity
    {
        public string PartitionKey { get; set; } = "CUSTOMER";
        public string RowKey { get; set; } = Guid.NewGuid().ToString("N");

        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public string Email { get; set; } = default!;

        public string Address { get; set; } = default!;

        public string? AttachmentName { get; set; }

        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
