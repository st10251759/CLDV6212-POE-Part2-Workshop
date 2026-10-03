using Azure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace PageTurn.Functions.Helpers;

/// <summary>Consistent error body returned by every endpoint.</summary>
public record ErrorResponse(int Status, string Error, IEnumerable<string>? Details = null);

/// <summary>Helper methods for 400/404/409/413/415/503/500 responses.</summary>
public static class ApiResults
{
    public static IActionResult BadRequest(string error, IEnumerable<string>? details = null) => Build(400, error, details);
    public static IActionResult NotFound(string error) => Build(404, error);
    public static IActionResult Conflict(string error) => Build(409, error);
    public static IActionResult PayloadTooLarge(string error) => Build(413, error);
    public static IActionResult UnsupportedMediaType(string error, IEnumerable<string>? details = null) => Build(415, error, details);

    /// <summary>Maps unexpected exceptions to a safe HTTP response and logs the detail.</summary>
    public static IActionResult FromException(ILogger logger, Exception ex)
    {
        switch (ex)
        {
            case RequestFailedException { Status: 412 }:
                return Conflict("The record was modified by another request. Please retry.");

            case RequestFailedException { Status: 0 }:
            case HttpRequestException:
                logger.LogError(ex, "Storage is unreachable.");
                return Build(503, "Storage service is unavailable. Check that Azurite is running.");

            case InvalidDataException:
            case BadHttpRequestException:
                return BadRequest("The request body could not be read. Check the multipart/form-data format.");

            default:
                logger.LogError(ex, "Unhandled exception.");
                return Build(500, "An unexpected error occurred.");
        }
    }

    private static IActionResult Build(int status, string error, IEnumerable<string>? details = null) =>
        new ObjectResult(new ErrorResponse(status, error, details)) { StatusCode = status };
}