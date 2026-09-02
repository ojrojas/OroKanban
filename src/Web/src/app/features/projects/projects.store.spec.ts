import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { patchState } from '@ngrx/signals';
import { setAllEntities } from '@ngrx/signals/entities';
import { ProjectsStore } from './projects.store';

describe('ProjectsStore', () => {
  let store: InstanceType<typeof ProjectsStore>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [ProjectsStore, provideHttpClient(), provideHttpClientTesting()] });
    store = TestBed.inject(ProjectsStore);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should have initial idle with empty entities', () => {
    expect(store.entities().length).toBe(0);
    expect(store.filter()).toBe('');
    expect(store.requestStatus()).toBe('idle');
    expect(store.total()).toBe(0);
  });

  it('load → pending → fulfilled with entities and computed filtered', async () => {
    store.setFilter('alpha');
    expect(store.filtered().length).toBe(0);
    const promise = store.load();
    expect(store.isPending()).toBe(true);
    const req = httpMock.expectOne(req => req.url.includes('/api/projects') && req.method === 'GET');
    expect(req.request.method).toBe('GET');
    req.flush({ items: [{ id: '1', name: 'Alpha Project', status: 'Active' }, { id: '2', name: 'Beta', status: 'Active' }] });
    await promise;
    expect(store.isFulfilled()).toBe(true);
    expect(store.entities().length).toBe(2);
    expect(store.filtered().length).toBe(1);
    expect(store.filtered()[0].name).toBe('Alpha Project');
  });

  it('load → error', async () => {
    const promise = store.load();
    const req = httpMock.expectOne(req => req.url.includes('/api/projects'));
    req.flush('fail', { status: 500, statusText: 'Error' });
    await promise;
    expect(store.error()).toBeTruthy();
  });

  it('switchMap via setFilter updates computed without extra request', async () => {
    const promise = store.load();
    const req = httpMock.expectOne(req => req.url.includes('/api/projects'));
    req.flush({ items: [{ id: '1', name: 'Alpha' }, { id: '2', name: 'Beta' }] });
    await promise;
    expect(store.filtered().length).toBe(2);
    store.setFilter('alpha');
    expect(store.filtered().length).toBe(1);
  });
});
