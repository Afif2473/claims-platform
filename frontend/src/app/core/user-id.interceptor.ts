import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { SessionService } from './session.service';

/** Adds the simulated identity header to every API call. Replaced by a bearer token with real auth. */
export const userIdInterceptor: HttpInterceptorFn = (req, next) => {
  const user = inject(SessionService).user();
  return next(user ? req.clone({ setHeaders: { 'X-User-Id': user.id } }) : req);
};
