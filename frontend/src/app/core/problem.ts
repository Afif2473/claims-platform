import { HttpErrorResponse } from '@angular/common/http';

/**
 * Turns an API error (RFC 7807 ProblemDetails, or ValidationProblemDetails for 400s)
 * into one readable line for the UI.
 */
export function problemMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) return 'Something went wrong.';
  if (error.status === 0) return 'Cannot reach the API. Is the backend running on port 5129?';

  const body = error.error as { title?: string; detail?: string; errors?: Record<string, string[]> } | null;
  if (body?.errors) return Object.values(body.errors).flat().join(' ');
  return body?.detail ?? body?.title ?? `Request failed (${error.status}).`;
}
