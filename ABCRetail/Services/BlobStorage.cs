using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace ABCRetail.Services
{
    public class BlobStorage
    {
        private readonly BlobServiceClient _blobService;

        public BlobStorage(IConfiguration config)
        {
            _blobService = new BlobServiceClient(config.GetConnectionString("AzureStorage"));
        }

        public async Task<string> UploadAsync(string containerName, IFormFile file, string? fileName = null)
        {
            var container = _blobService.GetBlobContainerClient(containerName);
            await container.CreateIfNotExistsAsync();

            fileName ??= Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName);
            var blob = container.GetBlobClient(fileName);

            using var stream = file.OpenReadStream();
            await blob.UploadAsync(stream, overwrite: true);

            return blob.Name; 
        }

        //HomeController to count blobs
        public async Task<BlobContainerClient> GetContainerAsync(string name)
        {
            var container = _blobService.GetBlobContainerClient(name);
            await container.CreateIfNotExistsAsync();
            return container;
        }

        //Blob Images
        public async Task<(Stream Stream, string ContentType)?> OpenReadAsync(string containerName, string blobName)
        {
            var container = _blobService.GetBlobContainerClient(containerName);
            var blob = container.GetBlobClient(blobName);

            if (!await blob.ExistsAsync()) return null;

            var dl = await blob.DownloadStreamingAsync();
            var ct = dl.Value.Details.ContentType ?? "application/octet-stream";
            return (dl.Value.Content, ct);
        }
    }
}
