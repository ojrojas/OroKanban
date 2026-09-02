import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from './api-client.service';
import { Paged, ListParams } from './paged.model';

export interface WorkItem { id: string; title: string; status: string; version: string; projectId: string; }

@Injectable({ providedIn: 'root' })
export class WorkItemsApi {
  private api = inject(ApiClient);
  list(params: ListParams = {}): Observable<Paged<WorkItem>> {
    return this.api.getPaged<WorkItem>('/api/work-items', params);
  }
  getDetail(id: string): Observable<any> {
    return this.api.get(`/api/work-items/${id}/detail`);
  }
  move(id: string, to: string, version: string) {
    return this.api.put(`/api/work-items/${id}/status`, { status: to, version }, version);
  }
}
