import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

export interface ProblemDetails {
  type: string;
  title: string;
  detail: string;
  status: number;
  code: string;
  currentVersion?: string;
}

export const problemDetailsInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      const problem = err.error as ProblemDetails;
      if (problem?.title) {
        console.error(`[ProblemDetails] ${problem.title}: ${problem.detail} (code=${problem.code})`);
        // Global toast would be shown here via ToastService
      }
      return throwError(() => err);
    })
  );
