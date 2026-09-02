import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';

export const etagInterceptor: HttpInterceptorFn = (req, next) => {
  const version = (req as any).version as string | undefined;
  const cloned = version ? req.clone({ setHeaders: { 'If-Match': `W/"${version}"` } }) : req;
  return next(cloned).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 409 || err.status === 412) {
        const problem = err.error as { currentVersion?: string; detail: string };
        console.warn(`Concurrency conflict: currentVersion=${problem.currentVersion}`);
        // Preserve edits, offer Reload/Merge via modal
      }
      return throwError(() => err);
    })
  );
};
