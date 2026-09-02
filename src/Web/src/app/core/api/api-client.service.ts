import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Paged, ListParams } from './paged.model';

@Injectable({ providedIn: 'root' })
export class ApiClient {
  private http = inject(HttpClient);

  getPaged<T>(url: string, params: ListParams = {}): Observable<Paged<T>> {
    let httpParams = new HttpParams();
    if (params.page) httpParams = httpParams.set('page', params.page);
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);
    if (params.q) httpParams = httpParams.set('q', params.q);
    if (params.filter) httpParams = httpParams.set('filter', params.filter);
    if (params.sort) httpParams = httpParams.set('sort', params.sort);
    if (params.sortDir) httpParams = httpParams.set('sortDir', params.sortDir);
    return this.http.get<Paged<T>>(url, { params: httpParams }).pipe(
      catchError((err: HttpErrorResponse) => throwError(() => err.error))
    );
  }

  get<T>(url: string): Observable<T> {
    return this.http.get<T>(url);
  }

  put<T>(url: string, body: any, version?: string) {
    const headers: any = {};
    if (version) headers['If-Match'] = `W/"${version}"`;
    return this.http.put<T>(url, body, { headers }).pipe(
      catchError((err: HttpErrorResponse) => throwError(() => err.error))
    );
  }

  post<T>(url: string, body: any) {
    return this.http.post<T>(url, body);
  }
}
