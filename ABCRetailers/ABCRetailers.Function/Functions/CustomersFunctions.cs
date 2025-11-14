using System.Net;
using System.Text.Json;
using ABCRetailers.Functions.Entities;
using ABCRetailers.Functions.Helpers;
using ABCRetailers.Functions.Models;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ABCRetailers.Functions.Functions;

public class CustomersFunctions
{
    private readonly TableServiceClient _tableService;
    private readonly ILogger<CustomersFunctions> _log;
    private readonly string _tableName;

    public CustomersFunctions(TableServiceClient tableService, ILogger<CustomersFunctions> log)
    {
        _tableService = tableService;
        _log = log;
        _tableName = Environment.GetEnvironmentVariable("TABLE_CUSTOMERS") ?? "Customers";
    }

    private TableClient GetClient()
    {
        var client = _tableService.GetTableClient(_tableName);
        client.CreateIfNotExists();
        return client;
    }

    [Function("Customers_List")]
    public async Task<HttpResponseData> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customers")] HttpRequestData req)
    {
        var client = GetClient();
        var results = new List<CustomerDto>();
        await foreach (var entity in client.QueryAsync<CustomerEntity>(e => e.PartitionKey == "Customer"))
            results.Add(Map.ToDto(entity));

        return await HttpJson.Ok(req, results);
    }

    [Function("Customers_Get")]
    public async Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customers/{id}")] HttpRequestData req,
        string id)
    {
        var client = GetClient();
        try
        {
            var resp = await client.GetEntityAsync<CustomerEntity>("Customer", id);
            return await HttpJson.Ok(req, Map.ToDto(resp.Value));
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return await HttpJson.Error(req, HttpStatusCode.NotFound, "Customer not found.");
        }
    }

    [Function("Customers_Create")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customers")] HttpRequestData req)
    {
        var client = GetClient();

        var dto = await JsonSerializer.DeserializeAsync<CustomerDto>(req.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (dto is null) return await HttpJson.Error(req, HttpStatusCode.BadRequest, "Invalid JSON.");

        var entity = new CustomerEntity();
        Map.Apply(dto, entity);

        await client.AddEntityAsync(entity);
        return await HttpJson.Ok(req, Map.ToDto(entity));
    }

    [Function("Customers_Update")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "customers/{id}")] HttpRequestData req,
        string id)
    {
        var client = GetClient();
        try
        {
            var existing = (await client.GetEntityAsync<CustomerEntity>("Customer", id)).Value;

            var dto = await JsonSerializer.DeserializeAsync<CustomerDto>(req.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dto is null) return await HttpJson.Error(req, HttpStatusCode.BadRequest, "Invalid JSON.");

            Map.Apply(dto, existing);
            await client.UpdateEntityAsync(existing, existing.ETag, TableUpdateMode.Replace);

            return await HttpJson.Ok(req, Map.ToDto(existing));
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return await HttpJson.Error(req, HttpStatusCode.NotFound, "Customer not found.");
        }
    }

    [Function("Customers_Delete")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "customers/{id}")] HttpRequestData req,
        string id)
    {
        var client = GetClient();
        try
        {
            await client.DeleteEntityAsync("Customer", id);
            return await HttpJson.Ok(req, new { deleted = true });
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return await HttpJson.Error(req, HttpStatusCode.NotFound, "Customer not found.");
        }
    }
}
