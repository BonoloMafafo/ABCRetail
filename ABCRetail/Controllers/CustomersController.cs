using System.Linq;
using System.IO;                       
using ABCRetail.Models;
using ABCRetail.Services;
using Microsoft.AspNetCore.Http;       
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.Controllers
{
    public class CustomersController : Controller
    {
        private readonly TableStorage _tables;
        private readonly QueueService _queues;
        private readonly FileStorage _files;

        public CustomersController(
            TableStorage tables,
            QueueService queues,
            FileStorage files)
        {
            _tables = tables;
            _queues = queues;
            _files = files;
        }

        
        public async Task<IActionResult> Index(string? q)
        {
            var items = await _tables.GetAllAsync<Customer>("Customers");

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qq = q.Trim().ToLowerInvariant();
                items = items.Where(c =>
                    ((c.FirstName ?? string.Empty).ToLower().Contains(qq)) ||
                    ((c.LastName ?? string.Empty).ToLower().Contains(qq)) ||
                    ((c.Email ?? string.Empty).ToLower().Contains(qq)) ||
                    ((c.Address ?? string.Empty).ToLower().Contains(qq))
                ).ToList();
            }

            ViewBag.Query = q;
            return View(items);
        }

        [HttpGet]
        public IActionResult Create() => View(new Customer());

        [HttpPost]
        public async Task<IActionResult> Create(Customer model, IFormFile? attachment)
        {
            if (!ModelState.IsValid) return View(model);

            model.PartitionKey = "CUSTOMER";
            model.RowKey = Guid.NewGuid().ToString("N");

            // Optional file upload to Azure File Share
            if (attachment != null && attachment.Length > 0)
            {
                var savedName = await _files.UploadFileAsync(
                    shareName: "customerfiles",
                    directory: "attachments",
                    file: attachment,
                    desiredFileName: $"{model.RowKey}{Path.GetExtension(attachment.FileName)}");

                model.AttachmentName = savedName;
            }

            // Save the entity
            await _tables.AddAsync("Customers", model);

            // Queue + log (non-blocking)
            try
            {
                var fullName = $"{model.FirstName} {model.LastName}".Trim();
                var msg = $"CustomerCreated; Name={fullName}; Email={model.Email}; " +
                          $"Address={model.Address}; File={(model.AttachmentName ?? "-")}; " +
                          $"At={DateTime.UtcNow:O}";

                await _queues.EnqueueAsync("orders", msg);
                await _files.AppendLogAsync("logs", "app", "events.log", msg);
            }
            catch (Exception ex)
            {
                TempData["err"] = "Saved the customer, but failed to queue/log: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> File(string id)
        {
            var customer = (await _tables.GetAllAsync<Customer>("Customers"))
                           .FirstOrDefault(c => c.RowKey == id);

            if (customer == null || string.IsNullOrWhiteSpace(customer.AttachmentName))
                return NotFound();

            var stream = await _files.OpenReadAsync("customerfiles", "attachments", customer.AttachmentName);
            if (stream == null) return NotFound();

            return File(stream, "application/octet-stream", customer.AttachmentName);
        }

    }
}
