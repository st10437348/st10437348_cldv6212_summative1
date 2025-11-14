using System.Net;
using Microsoft.Azure.Functions.Worker.Http;

namespace ABCRetailers.Functions.Helpers;

public static class HttpJson
{
    public static async Task<HttpResponseData> Ok(HttpRequestData req, object body)
    {
        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(body); 
        return res;
    }

    public static async Task<HttpResponseData> Error(HttpRequestData req, HttpStatusCode code, string message)
    {
        var res = req.CreateResponse(code);
        await res.WriteAsJsonAsync(new { error = message }); 
        return res;
    }
}


