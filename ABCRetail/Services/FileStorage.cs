using Azure;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace ABCRetail.Services
{
    public class FileStorage
    {
        private readonly ShareServiceClient _shareService;

        public FileStorage(IConfiguration config)
        {
            _shareService = new ShareServiceClient(config.GetConnectionString("AzureStorage"));
        }

        public async Task AppendLogAsync(string shareName, string directory, string fileName, string line)
        {
            var share = _shareService.GetShareClient(shareName);
            await share.CreateIfNotExistsAsync();

            var dir = await EnsureDirectoryPathAsync(share, directory);

            var file = dir.GetFileClient(fileName);
            if (!await file.ExistsAsync())
            {
                var options = new ShareFileCreateOptions
                {
                    HttpHeaders = new ShareFileHttpHeaders { ContentType = "text/plain" }
                };
                await file.CreateAsync(maxSize: 10 * 1024 * 1024, options: options); 
            }

            // Find next append offset from populated ranges
            var rangeInfo = await file.GetRangeListAsync(new HttpRange(0, null));
            long nextOffset = 0;
            var populated = rangeInfo.Value.Ranges ?? Enumerable.Empty<HttpRange>();
            if (populated.Any())
                nextOffset = populated.Max(r => r.Offset + (r.Length ?? 0));

            var bytes = Encoding.UTF8.GetBytes(line + Environment.NewLine);
            using var ms = new MemoryStream(bytes);

            if (nextOffset + bytes.Length > 10 * 1024 * 1024)
                throw new InvalidOperationException("events.log reached 10 MB. Increase size or rotate the file.");

            await file.UploadRangeAsync(new HttpRange(nextOffset, bytes.Length), ms);
        }

        public async Task<string> UploadFileAsync(
            string shareName,
            string directory,
            IFormFile file,
            string? desiredFileName = null)
        {
            if (file is null) throw new ArgumentNullException(nameof(file));

            var share = _shareService.GetShareClient(shareName);
            await share.CreateIfNotExistsAsync();

            var dir = await EnsureDirectoryPathAsync(share, directory);

            var safeName = desiredFileName ?? $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var fileClient = dir.GetFileClient(safeName);

            using var stream = file.OpenReadStream();

            // Create pre-sized file
            await fileClient.CreateAsync(stream.Length);

            // Upload full content (if any)
            if (stream.Length > 0)
            {
                await fileClient.UploadRangeAsync(new HttpRange(0, stream.Length), stream);
            }

            // Set Content-Type via options overload (SDK handles the rest)
            var ct = string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType;

            await fileClient.SetHttpHeadersAsync(
                new ShareFileSetHttpHeadersOptions
                {
                    HttpHeaders = new ShareFileHttpHeaders { ContentType = ct }
                });

            return safeName; 
        }

        public async Task<string> ReadAllAsync(string shareName, string directory, string fileName, CancellationToken ct = default)
        {
            var file = _shareService.GetShareClient(shareName)
                                    .GetDirectoryClient(directory)
                                    .GetFileClient(fileName);

            if (!await file.ExistsAsync(ct))
                return string.Empty;

            var download = await file.DownloadAsync(cancellationToken: ct);
            using var reader = new StreamReader(download.Value.Content, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }

        //Open a file for download/streaming
        public async Task<Stream?> OpenReadAsync(string shareName, string directory, string fileName, CancellationToken ct = default)
        {
            var file = _shareService.GetShareClient(shareName)
                                    .GetDirectoryClient(directory)
                                    .GetFileClient(fileName);

            if (!await file.ExistsAsync(ct))
                return null;

            var download = await file.DownloadAsync(cancellationToken: ct);
            return download.Value.Content; 
        }

        private static async Task<ShareDirectoryClient> EnsureDirectoryPathAsync(ShareClient share, string? path)
        {
            var current = share.GetRootDirectoryClient();
            if (string.IsNullOrWhiteSpace(path)) return current;

            foreach (var segment in path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries))
            {
                current = current.GetSubdirectoryClient(segment);
                await current.CreateIfNotExistsAsync();
            }
            return current;
        }
    }
}
