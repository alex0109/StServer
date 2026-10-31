using Application.Exceptions;
using Application.Import;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = MapException(exception);

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception occurred");
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode == StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred."
                : exception.Message,
            Instance = httpContext.Request.Path
        };

        if (exception is DuplicateEntryException duplicate)
        {
            problemDetails.Extensions["existingEntryId"] = duplicate.ExistingEntryId;
        }

        if (exception is ArticleImportException import)
        {
            problemDetails.Extensions["code"] = import.Code.ToString();
        }

        httpContext.Response.StatusCode = statusCode;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static (int StatusCode, string Title) MapException(Exception exception) =>
        exception switch
        {
            AiQuotaExceededException => (StatusCodes.Status429TooManyRequests, "AI quota exceeded"),
            AiUnavailableException => (StatusCodes.Status503ServiceUnavailable, "AI unavailable"),
            AiOutputException => (StatusCodes.Status422UnprocessableEntity, "AI could not generate"),
            EmailUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Email unavailable"),
            BillingUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Billing unavailable"),
            LimitExceededException => (StatusCodes.Status409Conflict, "Limit exceeded"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            ReauthRequiredException => (StatusCodes.Status403Forbidden, "Reauthentication required"),
            ArticleImportException => (StatusCodes.Status422UnprocessableEntity, "Import failed"),
            BadRequestException => (StatusCodes.Status400BadRequest, "Bad request"),
            // A body that isn't valid JSON for the endpoint is the caller's mistake, not a server error
            BadHttpRequestException badRequest => (badRequest.StatusCode, "Bad request"),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            _ => (StatusCodes.Status500InternalServerError, "Internal server error")
        };
}