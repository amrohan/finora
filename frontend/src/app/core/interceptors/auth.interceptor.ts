import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  let headers = req.headers;
  const token = authService.token();
  const householdId = authService.activeHouseholdId();

  if (token) {
    headers = headers.set('Authorization', `Bearer ${token}`);
  }

  if (householdId && !headers.has('X-Household-Id')) {
    headers = headers.set('X-Household-Id', householdId);
  }

  const clonedRequest = req.clone({ headers });

  return next(clonedRequest).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        authService.logout();
        router.navigate(['/auth/login']);
      }
      return throwError(() => error);
    })
  );
};

