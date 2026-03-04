using Logging.Constant;
using Logging.Interface;
using Logging.Models;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Text.Json;

namespace Logging.Middleware;

public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILoggerService _logger;

    public RequestResponseLoggingMiddleware(
        RequestDelegate next,
        ILoggerService logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestTime = DateTime.Now;
        var logDetails = new RequestLogDetails();
        var requestId = context.TraceIdentifier;
        try
        {
            if (!context.Request.Headers.ContainsKey(RequestHeadersConst.RequestId))
            {
                context.Request.Headers[RequestHeadersConst.RequestId] = requestId;
            }

            requestId = context.Request.Headers[RequestHeadersConst.RequestId];

            context.Request.Headers[RequestHeadersConst.ServiceOrigin] = Convert.ToString(context.Request.Path);

            if (context.Request.Headers.ContainsKey(RequestHeadersConst.CacheEnabled))
            {
                if (context.Request.Headers.TryGetValue(RequestHeadersConst.CacheEnabled, out var cacheEnabledValues))
                {
                    if (bool.TryParse(cacheEnabledValues.FirstOrDefault(), out bool cacheEnabled))
                        logDetails.IsCacheEnabled = cacheEnabled;
                }
            }

            // Capture the request information
            var request = context.Request;
            logDetails.URL = request.Path + request.QueryString;
            context.Request.Headers.TryGetValue(RequestHeadersConst.XForwardedFor, out var forwardedIp);
            logDetails.ClientIp = !string.IsNullOrEmpty(forwardedIp) ? forwardedIp : Convert.ToString(context.Connection?.RemoteIpAddress);
            logDetails.RequestTime = requestTime;

            // Buffer the request body for logging and future reads
            context.Request.EnableBuffering();
            using (var buffer = new MemoryStream())
            {
                await request.Body.CopyToAsync(buffer);
                logDetails.Request = Encoding.UTF8.GetString(buffer.ToArray());
                request.Body.Position = 0; // Reset the request body stream position
            }

            Stream originalResponseBody = context.Response.Body;
            using var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;

            await _next(context); // Call the next middleware

            // Capture the response body
            responseBodyStream.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(responseBodyStream).ReadToEndAsync();
            responseBodyStream.Seek(0, SeekOrigin.Begin);

            logDetails.Response = responseBody;
            logDetails.ResponseTime = DateTime.Now;
            logDetails.HttpStatusCode = context.Response?.StatusCode;
            logDetails.RequestId = requestId;
            logDetails.Headers = JsonSerializer.Serialize(request.Headers);


            // Restore the original response body
            await responseBodyStream.CopyToAsync(originalResponseBody);
        }
        finally
        {
            _ = _logger.Info(logDetails);
        }
    }
}
