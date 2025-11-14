using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Net.Http.Headers;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;


namespace ABCRetailers.Functions.Helpers;

public static class MultipartHelper
{
    public static bool IsMultipartContentType(string? contentType)
        => !string.IsNullOrEmpty(contentType)
           && contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0;

    public static string? GetBoundary(string contentType)
    {
        var elements = contentType?.Split(';') ?? Array.Empty<string>();
        var boundary = elements.Select(e => e.Trim())
                               .FirstOrDefault(v => v.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase));
        return boundary?.Substring("boundary=".Length)?.Trim('"');
    }

    public static async Task<(Dictionary<string, string> fields, (string FileName, byte[] Bytes)? file)>
        ReadFormAsync(HttpRequestData req, string fileFieldName = "imageFile")
    {
        var contentType = req.Headers.GetValues("Content-Type").FirstOrDefault()
                         ?? req.Headers.GetValues("content-type").FirstOrDefault();

        if (!IsMultipartContentType(contentType))
            throw new InvalidOperationException("Expected multipart/form-data");

        var boundary = GetBoundary(contentType) ?? throw new InvalidOperationException("Missing boundary");
        var reader = new MultipartReader(boundary, req.Body);

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        (string, byte[])? file = null;

        MultipartSection? section;
        while ((section = await reader.ReadNextSectionAsync()) != null)
        {
            var hasContentDispositionHeader = ContentDispositionHeaderValue.TryParse(
                section.ContentDisposition, out var contentDisposition);

            if (!hasContentDispositionHeader) continue;

            if (contentDisposition!.IsFileDisposition())
            {
                var fileNameSeg = !StringSegment.IsNullOrEmpty(contentDisposition.FileNameStar)
                    ? contentDisposition.FileNameStar
                    : contentDisposition.FileName;

                var fileName = HeaderUtilities.RemoveQuotes(fileNameSeg).Value ?? "file";


                using var ms = new MemoryStream();
                await section.Body.CopyToAsync(ms);
                file = (fileName, ms.ToArray());
            }
            else if (contentDisposition.IsFormDisposition())
            {
                var fieldName = HeaderUtilities.RemoveQuotes(contentDisposition.Name).Value ?? "";
                using var sr = new StreamReader(section.Body, Encoding.UTF8);
                var fieldValue = await sr.ReadToEndAsync();
                fields[fieldName] = fieldValue;
            }
        }

        return (fields, file);
    }
}

