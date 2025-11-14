using System.Net;
using System.Text.Json;
using ABCRetailers.Functions.Entities;
using ABCRetailers.Functions.Helpers;
using ABCRetailers.Functions.Models;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ABCRetailers.Functions.Functions;

public class ProductsFunctions
{
    private readonly TableServiceClient _tableService;
    private readonly BlobServiceClient _blobService;
    private readonly ILogger<ProductsFunctions> _log;
    private readonly string _tableName;
    private readonly string _imagesContainer;

    public ProductsFunctions(
        TableServiceClient tableService,
        BlobServiceClient blobService,
        ILogger<ProductsFunctions> log)
    {
        _tableService = tableService;
        _blobService = blobService;
        _log = log;
        _tableName = Environment.GetEnvironmentVariable("TABLE_PRODUCTS") ?? "Products";
        _imagesContainer = Environment.GetEnvironmentVariable("BLOB_IMAGES_CONTAINER") ?? "product-images";
    }

    private TableClient Table()
    {
        var c = _tableService.GetTableClient(_tableName);
        c.CreateIfNotExists();
        return c;
    }

    private BlobContainerClient Images()
    {
        var c = _blobService.GetBlobContainerClient(_imagesContainer);
        c.CreateIfNotExists();
        return c;
    }

    [Function("Products_List")]
    public async Task<HttpResponseData> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "products")] HttpRequestData req)
    {
        var client = Table();
        var items = new List<ProductDto>();
        await foreach (var e in client.QueryAsync<ProductEntity>(e => e.PartitionKey == "Product"))
            items.Add(Map.ToDto(e));
        return await HttpJson.Ok(req, items);
    }

    [Function("Products_Get")]
    public async Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "products/{id}")] HttpRequestData req,
        string id)
    {
        var client = Table();
        try
        {
            var e = (await client.GetEntityAsync<ProductEntity>("Product", id)).Value;
            return await HttpJson.Ok(req, Map.ToDto(e));
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return await HttpJson.Error(req, HttpStatusCode.NotFound, "Product not found.");
        }
    }

    [Function("Products_Create")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "products")] HttpRequestData req)
    {
        var tbl = Table();
        var product = new ProductEntity();

        var contentType = req.Headers.TryGetValues("Content-Type", out var vals) ? vals.FirstOrDefault() : null;

        if (MultipartHelper.IsMultipartContentType(contentType))
        {
            var (fields, file) = await MultipartHelper.ReadFormAsync(req, "imageFile");
            var dto = new ProductDto
            {
                productName = fields.GetValueOrDefault("productName") ?? "",
                description = fields.GetValueOrDefault("description") ?? "",
                price = double.TryParse(fields.GetValueOrDefault("price"), out var p) ? p : 0,
                stockAvailable = int.TryParse(fields.GetValueOrDefault("stockAvailable"), out var s) ? s : 0
            };
            Map.Apply(dto, product);

            if (file.HasValue && file.Value.Bytes.Length > 0)
            {
                var blob = Images().GetBlobClient($"{product.RowKey}-{file.Value.FileName}");
                await blob.UploadAsync(new BinaryData(file.Value.Bytes), overwrite: true);
                product.ImageUrl = blob.Uri.ToString();
            }
        }
        else
        {
            var dto = await JsonSerializer.DeserializeAsync<ProductDto>(req.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dto is null) return await HttpJson.Error(req, HttpStatusCode.BadRequest, "Invalid JSON.");
            Map.Apply(dto, product);
        }

        await tbl.AddEntityAsync(product);
        return await HttpJson.Ok(req, Map.ToDto(product));
    }

    [Function("Products_Update")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "products/{id}")] HttpRequestData req,
        string id)
    {
        var tbl = Table();
        try
        {
            var existing = (await tbl.GetEntityAsync<ProductEntity>("Product", id)).Value;
            var contentType = req.Headers.TryGetValues("Content-Type", out var vals) ? vals.FirstOrDefault() : null;

            if (MultipartHelper.IsMultipartContentType(contentType))
            {
                var (fields, file) = await MultipartHelper.ReadFormAsync(req, "imageFile");
                var dto = new ProductDto
                {
                    productName = fields.GetValueOrDefault("productName") ?? existing.ProductName,
                    description = fields.GetValueOrDefault("description") ?? existing.Description,
                    price = double.TryParse(fields.GetValueOrDefault("price"), out var p) ? p : existing.Price,
                    stockAvailable = int.TryParse(fields.GetValueOrDefault("stockAvailable"), out var s) ? s : existing.StockAvailable,
                    imageUrl = existing.ImageUrl
                };
                Map.Apply(dto, existing);

                if (file.HasValue && file.Value.Bytes.Length > 0)
                {
                    var blob = Images().GetBlobClient($"{existing.RowKey}-{file.Value.FileName}");
                    await blob.UploadAsync(new BinaryData(file.Value.Bytes), overwrite: true);
                    existing.ImageUrl = blob.Uri.ToString();
                }
            }
            else
            {
                var dto = await JsonSerializer.DeserializeAsync<ProductDto>(req.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (dto is null) return await HttpJson.Error(req, HttpStatusCode.BadRequest, "Invalid JSON.");
                Map.Apply(dto, existing);
            }

            await tbl.UpdateEntityAsync(existing, existing.ETag, TableUpdateMode.Replace);
            return await HttpJson.Ok(req, Map.ToDto(existing));
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return await HttpJson.Error(req, HttpStatusCode.NotFound, "Product not found.");
        }
    }

    [Function("Products_Delete")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "products/{id}")] HttpRequestData req,
        string id)
    {
        var tbl = Table();
        try
        {
            await tbl.DeleteEntityAsync("Product", id);
            return await HttpJson.Ok(req, new { deleted = true });
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return await HttpJson.Error(req, HttpStatusCode.NotFound, "Product not found.");
        }
    }
}



