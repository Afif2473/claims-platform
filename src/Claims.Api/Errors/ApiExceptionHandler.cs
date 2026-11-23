using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Claims.Api.Errors;

/// <summary>
/// Translates exceptions into RFC 7807 ProblemDetails responses, so every error the
/// frontend receives has the same shape: { status, title, detail }.
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            UnauthenticatedException e => (StatusCodes.Status401Unauthorized, "Unauthenticated", e.Message),
            ForbiddenException e => (StatusCodes.Status403Forbidden, "Forbidden", e.Message),
            NotFoundException e => (StatusCodes.Status404NotFound, "Not found", e.Message),
            BusinessRuleException e => (StatusCodes.Status409Conflict, "Business rule violation", e.Message),
            ConcurrencyConflictException e => (StatusCodes.Status409Conflict, "Concurrency conflict", e.Message),
            // Raised by EF when another request saved between our read and our write.
            DbUpdateConcurrencyException =>(StatusCodes.Status409Conflict, "Concurrency conflict",
                "The claim was changed by someone else. Reload it and try again."),
            // Never leak internal exception messages to the client.
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error", "An unexpected error occurred.")
        };

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = status;
        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail }
        });
    }
}
