import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { DashboardStore } from './dashboard.store';

describe('DashboardStore', () => {
  let store: InstanceType<typeof DashboardStore>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [DashboardStore, provideHttpClient(), provideHttpClientTesting()]
    });
    store = TestBed.inject(DashboardStore);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should have initial idle state', () => {
    expect(store.kpis()).toEqual([]);
    expect(store.requestStatus()).toBe('idle');
    expect(store.isPending()).toBe(false);
    expect(store.overdue()).toBe(0);
    expect(store.hasKpis()).toBe(false);
  });

  it('load → pending → fulfilled with kpis and computed overdue', async () => {
    const promise = store.load();
    expect(store.isPending()).toBe(true);
    const req = httpMock.expectOne('/api/dashboard/kpis');
    expect(req.request.method).toBe('GET');
    req.flush([
      { key: 'myProjects', value: 3, link: '/projects' },
      { key: 'overdue', value: 2, link: '/my-tasks?filter=overdue' },
      { key: 'blocked', value: 1, link: '/kanban?filter=blocked' }
    ]);
    await promise;
    expect(store.isFulfilled()).toBe(true);
    expect(store.kpis().length).toBe(3);
    expect(store.overdue()).toBe(2);
    expect(store.blocked()).toBe(1);
    expect(store.totalProjects()).toBe(3);
    expect(store.hasKpis()).toBe(true);
    expect(store.error()).toBeNull();
  });

  it('load → error on HTTP failure', async () => {
    const promise = store.load();
    const req = httpMock.expectOne('/api/dashboard/kpis');
    req.flush('fail', { status: 500, statusText: 'Server Error' });
    await promise;
    expect(store.isPending()).toBe(false);
    expect(store.error()).toBeTruthy();
    expect(store.isFulfilled()).toBe(false);
  });
});
