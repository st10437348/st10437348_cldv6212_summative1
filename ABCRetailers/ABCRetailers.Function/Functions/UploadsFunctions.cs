using ABCRetailers.Functions.Helpers;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace ABCRetailers.Functions.Functions;

public class UploadsFunctions
{
    private readonly BlobServiceClient _blobService;
    private readonly ShareServiceClient _shareService;
    private readonly ILogger<UploadsFunctions> _log;

    private readonly string _proofsContainer;
    private readonly string _shareName;
    private readonly string _shareDir;

    public UploadsFunctions(
        BlobServiceClient blobService,
        ShareServiceClient shareService,
        ILogger<UploadsFunctions> log)
    {
        _blobService = blobService;
        _shareService = shareService;
        _log = log;

        _proofsContainer = Environment.GetEnvironmentVariable("BLOB_PROOFS_CONTAINER") ?? "payment-proofs";
        _shareName = Environment.GetEnvironmentVariable("FILESHARE_CONTRACTS") ?? "contracts";
        _shareDir = Environment.GetEnvironmentVariable("FILESHARE_CONTRACTS_DIR") ?? "payments";
    }

    private BlobContainerClient ProofsContainer()
    {
        var c = _blobService.GetBlobContainerClient(_proofsContainer);
        c.CreateIfNotExists();
        return c;
    }

    private ShareDirectoryClient ShareDir()
    {
        var share = _shareService.GetShareClient(_shareName);
        share.CreateIfNotExists();
        var dir = share.GetDirectoryClient(_shareDir);
        dir.CreateIfNotExists();
        return dir;
    }

    [Function("Uploads_ProofOfPayment")]
    public async Task<HttpResponseData> ProofOfPayment(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "uploads/proof-of-payment")] HttpRequestData req)
    {
        try
        {
            var (fields, file) = await MultipartHelper.ReadFormAsync(req, "ProofOfPayment");
            if (file is null || file.Value.Bytes.Length == 0)
                return await Helpers.HttpJson.Error(req, HttpStatusCode.BadRequest, "Please attach a ProofOfPayment file.");

            var orderId = fields.GetValueOrDefault("OrderId");
            var customerName = fields.GetValueOrDefault("CustomerName");

            var safeOrder = string.IsNullOrWhiteSpace(orderId) ? "noorder" : orderId.Trim();
            var stamped = $"{safeOrder}_{DateTime.UtcNow.Ticks}_{file.Value.FileName}";
            var fileName = stamped.Replace(" ", "_");

            var blob = ProofsContainer().GetBlobClient(fileName);
            await blob.UploadAsync(new BinaryData(file.Value.Bytes), overwrite: true);
            var blobUrl = blob.Uri.ToString();

            var dir = ShareDir();
            var shareFile = dir.GetFileClient(fileName);
            await shareFile.CreateAsync(file.Value.Bytes.Length);
            using (var ms = new MemoryStream(file.Value.Bytes))
            {
                await shareFile.UploadRangeAsync(new HttpRange(0, ms.Length), ms);
            }

            var fileSharePath = $"{_shareName}/{_shareDir}/{fileName}";

            _log.LogInformation("Proof of payment uploaded. Blob: {BlobUrl}, FileShare: {Path}", blobUrl, fileSharePath);

            return await Helpers.HttpJson.Ok(req, new
            {
                fileName,
                blobUrl,
                fileSharePath,
                orderId,
                customerName
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Upload failed");
            return await Helpers.HttpJson.Error(req, HttpStatusCode.InternalServerError, ex.Message);
        }
    }
}
