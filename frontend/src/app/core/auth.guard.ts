import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionService } from './session.service';

/** Sends the visitor to the user picker until a user is chosen. Authorisation itself is enforced by the API. */
export const authGuard: CanActivateFn = () =>
  inject(SessionService).user() ? true : inject(Router).createUrlTree(['/login']);
