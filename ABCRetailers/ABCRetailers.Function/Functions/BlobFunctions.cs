using ABCRetailers.Functions.Entities;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace ABCRetailers.Functions.Functions;

public class BlobFunctions
{
    private readonly BlobServiceClient _blobService;
    private readonly TableServiceClient _tableService;
    private readonly ILogger<BlobFunctions> _log;
    private readonly string _imagesContainer;
    private readonly string _productsTable;

    public BlobFunctions(
        BlobServiceClient blobService,
        TableServiceClient tableService,
        ILogger<BlobFunctions> log)
    {
        _blobService = blobService;
        _tableService = tableService;
        _log = log;

        _imagesContainer = Environment.GetEnvironmentVariable("BLOB_IMAGES_CONTAINER") ?? "product-images";
        _productsTable = Environment.GetEnvironmentVariable("TABLE_PRODUCTS") ?? "Products";
    }

    private BlobContainerClient Images()
    {
        var c = _blobService.GetBlobContainerClient(_imagesContainer);
        c.CreateIfNotExists();
        return c;
    }

    private TableClient Products()
    {
        var t = _tableService.GetTableClient(_productsTable);
        t.CreateIfNotExists();
        return t;
    }

    private static string? TryGetGuidPrefix(string blobName)
    {
        if (string.IsNullOrWhiteSpace(blobName) || blobName.Length < 36)
            return null;

        var candidate = blobName.Substring(0, 36);
        return Guid.TryParse(candidate, out var guid) ? guid.ToString() : null;
    }

    // Fires when a blob is uploaded to the images container
    [Function("OnProductImageUploaded")]
    public async Task Run(
        [BlobTrigger("%BLOB_IMAGES_CONTAINER%/{name}", Connection = "STORAGE_CONNECTION")] byte[] content,
        string name)
    {
        var container = Images();
        var blob = container.GetBlobClient(name);

        long size = content?.LongLength ?? 0;
        try
        {
            var props = await blob.GetPropertiesAsync();
            size = props.Value.ContentLength;
        }
        catch { }

        _log.LogInformation("Image uploaded: {Name} ({Bytes} bytes)", name, size);

        var productId = TryGetGuidPrefix(name);
        if (productId is null)
        {
            _log.LogWarning("Blob name doesn't start with a valid GUID: {Name}", name);
            return;
        }

        try
        {
            var products = Products();
            var entity = (await products.GetEntityAsync<ProductEntity>("Product", productId)).Value;

            var blobUrl = blob.Uri.ToString();
            if (!string.Equals(entity.ImageUrl, blobUrl, StringComparison.OrdinalIgnoreCase))
            {
                entity.ImageUrl = blobUrl;
                await products.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace);
                _log.LogInformation("Updated Product {Id} ImageUrl.", productId);
            }
            else
            {
                _log.LogInformation("Product {Id} already has this ImageUrl.", productId);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _log.LogWarning("Product {Id} not found for image {Name}.", productId, name);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed processing OnProductImageUploaded for {Name}", name);
        }
    }
}
