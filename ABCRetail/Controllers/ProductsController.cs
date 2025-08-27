using System.Linq; 
using ABCRetail.Models;
using ABCRetail.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.Controllers
{
    public class ProductsController : Controller
    {
        private readonly TableStorage _tables;
        private readonly BlobStorage _blobs;
        private readonly QueueService _queues;
        private readonly FileStorage _files;

        public ProductsController(
            TableStorage tables,
            BlobStorage blobs,
            QueueService queues,
            FileStorage files)
        {
            _tables = tables;
            _blobs = blobs;
            _queues = queues;
            _files = files;
        }

        public async Task<IActionResult> Index(string? q)
        {
            var items = await _tables.GetAllAsync<Product>("Products");

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qq = q.Trim().ToLowerInvariant();
                items = items.Where(p =>
                    ((p.Name ?? string.Empty).ToLower().Contains(qq)) ||
                    ((p.StockKeepingUnit ?? string.Empty).ToLower().Contains(qq))
                ).ToList();
            }

            ViewBag.Query = q;
            return View(items);
        }

        [HttpGet]
        public IActionResult Create() => View(new Product());

        [HttpPost]
        public async Task<IActionResult> Create(Product model, IFormFile? image)
        {
            if (!ModelState.IsValid) return View(model);

            model.PartitionKey = "PRODUCT";
            model.RowKey = Guid.NewGuid().ToString("N");

            if (image != null && image.Length > 0)
            {
                var blobName = await _blobs.UploadAsync("product-images", image);
                model.ImageName = blobName;
            }

            await _tables.AddAsync("Products", model);

            // Log/queue (won't block if they fail)
            try
            {
                var msg = $"ProductCreated; StockKeepingUnit={model.StockKeepingUnit}; Name={model.Name}; Image={model.ImageName ?? "-"}; At={DateTime.UtcNow:O}";
                await _queues.EnqueueAsync("orders", msg);
                await _files.AppendLogAsync("logs", "app", "events.log", msg);
            }
            catch (Exception ex)
            {
                TempData["err"] = "Saved product, but failed to queue/log: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Image(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var result = await _blobs.OpenReadAsync("product-images", id);
            if (result == null) return NotFound();

            return File(result.Value.Stream, result.Value.ContentType);
        }
    }
}
