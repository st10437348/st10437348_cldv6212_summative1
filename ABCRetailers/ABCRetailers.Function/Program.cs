using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Files.Shares;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        var conn = Environment.GetEnvironmentVariable("STORAGE_CONNECTION");
        if (string.IsNullOrWhiteSpace(conn))
            throw new InvalidOperationException("STORAGE_CONNECTION is not set.");

        services.AddSingleton(new TableServiceClient(conn));
        services.AddSingleton(new BlobServiceClient(conn));

        var qOpts = new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 };
        services.AddSingleton(new QueueServiceClient(conn, qOpts));

        services.AddSingleton(new ShareServiceClient(conn));
    })
    .Build();

await host.RunAsync();



