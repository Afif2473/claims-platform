namespace Claims.Api.Errors;

// Services throw these; ApiExceptionHandler maps each one to an HTTP status.
// This keeps HTTP concerns out of the service layer and status-code logic out of controllers.

/// <summary>401 — no valid X-User-Id was supplied.</summary>
public class UnauthenticatedException(string message) : Exception(message);

/// <summary>403 — the user is known but their role may not perform this action.</summary>
public class ForbiddenException(string message) : Exception(message);

/// <summary>404 — the resource does not exist, or the user is not allowed to know it exists.</summary>
public class NotFoundException(string message) : Exception(message);

/// <summary>409 — the request is well-formed but conflicts with the claim's current state.</summary>
public class BusinessRuleException(string message) : Exception(message);

/// <summary>409 — the client acted on a stale Version of the claim.</summary>
public class ConcurrencyConflictException(string message) : Exception(message);
