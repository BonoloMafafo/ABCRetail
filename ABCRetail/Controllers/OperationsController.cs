using ABCRetail.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.Controllers
{
    public class OperationsController : Controller
    {
        private readonly QueueService _queues;
        private readonly FileStorage _files;

        public OperationsController(QueueService queues, FileStorage files)
        {
            _queues = queues;
            _files = files;
        }

        [HttpGet]
        public IActionResult Index() => View();

        [HttpPost]
        public async Task<IActionResult> EnqueueAndLog(string sku, string imageName = "")
        {
            var msg = $"Processing order for Stock Keeping Unit={sku}, Image={imageName}, At={DateTime.UtcNow:O}";
            await _queues.EnqueueAsync("orders", msg);

            await _files.AppendLogAsync(
                shareName: "logs",
                directory: "app",
                fileName: "events.log",
                line: msg);

            TempData["ok"] = "Message queued and log written.";
            return RedirectToAction(nameof(Index));
        }
    }
}
