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

        // What would let the request run: the client opens the matching upgrade or credits offer
        if (exception is AiUsageDeniedException denied)
        {
            problemDetails.Extensions["code"] = denied.Code switch
            {
                AiDenialCode.QuotaExhausted => "quota_exhausted",
                AiDenialCode.InsufficientCredits => "insufficient_credits",
                AiDenialCode.TrialUsed => "trial_used",
                AiDenialCode.AccountRequired => "account_required",
                _ => "input_too_large",
            };
            problemDetails.Extensions["feature"] = denied.Feature.ToString();
            problemDetails.Extensions["bucket"] = denied.Bucket.ToString();
            problemDetails.Extensions["remaining"] = denied.Remaining;
            problemDetails.Extensions["creditsRequired"] = denied.CreditsRequired;
            problemDetails.Extensions["creditBalance"] = denied.CreditBalance;
            if (denied.MaxInputChars is { } max) problemDetails.Extensions["maxInputChars"] = max;
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
            AiUsageDeniedException => (StatusCodes.Status402PaymentRequired, "AI usage denied"),
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