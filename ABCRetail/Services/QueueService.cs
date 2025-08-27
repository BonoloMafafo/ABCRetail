using Azure.Storage.Queues;

namespace ABCRetail.Services
{
    public class QueueService
    {
        private readonly QueueServiceClient _queueService;
        private readonly string _connectionString;

        public QueueService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("AzureStorage");
            _queueService = new QueueServiceClient(_connectionString);
        }

        public async Task EnqueueAsync(string queueName, string message)
        {
            var queue = _queueService.GetQueueClient(queueName);
            await queue.CreateIfNotExistsAsync();
            await queue.SendMessageAsync(message);
        }

        //HomeController stats (ApproximateMessagesCount, etc.)
        public QueueClient GetClient(string name)
        {
            var qc = new QueueClient(_connectionString, name);
            qc.CreateIfNotExists();
            return qc;
        }

        // (Optional) direct helper if you prefer not to call GetClient in the controller
        public async Task<int> GetApproximateCountAsync(string name)
        {
            var qc = _queueService.GetQueueClient(name);
            await qc.CreateIfNotExistsAsync();
            var props = await qc.GetPropertiesAsync();
            return props.Value.ApproximateMessagesCount;
        }
    }
}
