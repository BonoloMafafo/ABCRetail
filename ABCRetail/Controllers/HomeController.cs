using System.Diagnostics;
using ABCRetail.Models;
using ABCRetail.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly TableStorage _tables;
        private readonly BlobStorage _blobs;
        private readonly QueueService _queues;

        public HomeController(
            ILogger<HomeController> logger,
            TableStorage tables,
            BlobStorage blobs,
            QueueService queues)
        {
            _logger = logger;
            _tables = tables;
            _blobs = blobs;
            _queues = queues;
        }

        public async Task<IActionResult> Index()
        {
            // Defaults (avoid breaking the page if anything fails)
            int productCount = 0, customerCount = 0, blobCount = 0, queueSize = 0;

            try
            {
                // Tables
                var products = await _tables.GetAllAsync<Product>("Products");
                var customers = await _tables.GetAllAsync<Customer>("Customers");
                productCount = products.Count;
                customerCount = customers.Count;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read table counts");
            }

            try
            {
                // Blobs (product images)
                var container = await _blobs.GetContainerAsync("product-images");
                await foreach (var _ in container.GetBlobsAsync())
                    blobCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to count blobs");
            }

            try
            {
                // Queue size (approximate)
                var q = _queues.GetClient("orders");
                var props = await q.GetPropertiesAsync();
                queueSize = props.Value.ApproximateMessagesCount;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read queue size");
            }

            ViewBag.ProductCount = productCount;
            ViewBag.CustomerCount = customerCount;
            ViewBag.BlobCount = blobCount;
            ViewBag.QueueSize = queueSize;

            return View();
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
