// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName=Thumbnail

// Learn how to locally debug an Event Grid-triggered function:
//    https://aka.ms/AA30pjh

// Use for local testing:
//   https://{ID}.ngrok.io/runtime/webhooks/EventGrid?functionName=Thumbnail

using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ImageFunctions
{
    public static class Thumbnail
    {
        private static readonly string BLOB_STORAGE_SERVICE_URL = Environment.GetEnvironmentVariable("STORAGE_SERVICE_URL");

        private static string GetBlobNameFromUrl(string bloblUrl)
        {
            var uri = new Uri(bloblUrl);
            var blobClient = new BlobClient(uri);
            return blobClient.Name;
        }

        private static string GetExtensionFromUrl(string url)
        {
            var uri = new Uri(url);
            var extension = Path.GetExtension(uri.LocalPath);
            return extension;
        }

        private static IImageEncoder GetEncoder(string extension)
        {
            IImageEncoder encoder = null;

            extension = extension.Replace(".", "");

            var isSupported = Regex.IsMatch(extension, "gif|png|jpe?g", RegexOptions.IgnoreCase);

            if (isSupported)
            {
                switch (extension.ToLower())
                {
                    case "png":
                        encoder = new PngEncoder();
                        break;
                    case "jpg":
                        encoder = new JpegEncoder();
                        break;
                    case "jpeg":
                        encoder = new JpegEncoder();
                        break;
                    case "gif":
                        encoder = new GifEncoder();
                        break;
                    default:
                        break;
                }
            }

            return encoder;
        }

        [FunctionName("Thumbnail")]
        public static async Task Run(
            [EventGridTrigger]EventGridEvent eventGridEvent,
            [Blob("{data.url}", FileAccess.Read)] Stream input,
            ILogger log)
        {
            try
            {
                if (input != null)
                {
                    var createdEvent = ((JObject)eventGridEvent.Data).ToObject<StorageBlobCreatedEventData>();
                    var extension = Path.GetExtension(createdEvent.Url);
                    var encoder = GetEncoder(extension);

                    if (encoder != null)
                    {
                        var thumbnailWidths = Environment.GetEnvironmentVariable("THUMBNAIL_WIDTHS").Split(',');
                        var thumbContainerName = Environment.GetEnvironmentVariable("THUMBNAIL_CONTAINER_NAME");
                        var blobServiceClient = new BlobServiceClient(
                            new Uri(BLOB_STORAGE_SERVICE_URL.TrimEnd('/')), // BLOB_STORAGE_SERVICE_URL typically looks like this, https://owplocalimages.blob.core.windows.net/, so trim the extra /
                            new DefaultAzureCredential());
                        var blobContainerClient = blobServiceClient.GetBlobContainerClient(thumbContainerName);
                        var blobName = GetBlobNameFromUrl(createdEvent.Url);
                        var blobNameWithoutExtension = Path.GetFileNameWithoutExtension(blobName);

                        foreach (var widthString in thumbnailWidths)
                        {
                            if (!int.TryParse(widthString, out int width))
                                continue;

                            var thumbnailBlobName = $"{blobNameWithoutExtension}_{width}{extension}";

                            input.Position = 0; // Reset the position of the input stream. Without this, second image processing will fail.
                            using (var output = new MemoryStream())
                            using (Image<Rgba32> image = Image.Load(input))
                            {
                                var divisor = (decimal)image.Width / width;
                                var height = Convert.ToInt32(Math.Round((decimal)(image.Height / divisor)));

                                image.Mutate(x => x.Resize(width, height));
                                image.Save(output, encoder);
                                output.Position = 0;
                                await blobContainerClient.UploadBlobAsync(thumbnailBlobName, output);
                            }
                        }
                    }
                    else
                    {
                        log.LogInformation($"No encoder support for: {createdEvent.Url}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogInformation(ex.Message);
                throw;
            }
        }
    }
}
