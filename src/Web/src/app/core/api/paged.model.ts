export interface Paged<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface ProblemDetails {
  type: string;
  title: string;
  detail: string;
  status: number;
  code: string;
  currentVersion?: string;
}

export interface ListParams {
  page?: number;
  pageSize?: number;
  q?: string;
  filter?: string;
  sort?: string;
  sortDir?: 'asc' | 'desc';
}
