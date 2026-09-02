import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { WorkItemDetailStore } from './work-item-detail.store';

describe('WorkItemDetailStore', () => {
  let store: InstanceType<typeof WorkItemDetailStore>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [WorkItemDetailStore, provideHttpClient(), provideHttpClientTesting()] });
    store = TestBed.inject(WorkItemDetailStore);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should have null item initially and not loaded', () => {
    expect(store.item()).toBeNull();
    expect(store.isLoaded()).toBe(false);
    expect(store.progressExplanation()).toBeNull();
  });

  it('load → pending → fulfilled with progressExplanation computed', async () => {
    const promise = store.load('w1');
    expect(store.isPending()).toBe(true);
    const req = httpMock.expectOne('/api/work-items/w1/detail');
    expect(req.request.method).toBe('GET');
    req.flush({ id: 'w1', title: 'T1', progress: 66, subtasks: [{ id: 's1', done: true }, { id: 's2', done: true }, { id: 's3', done: false }], metrics: [{ key: 'm1' }] });
    await promise;
    expect(store.isFulfilled()).toBe(true);
    expect(store.item()?.progress).toBe(66);
    expect(store.progressExplanation()?.breakdown).toContain('subtasks 2/3');
    expect(store.isLoaded()).toBe(true);
  });

  it('load → error', async () => {
    const promise = store.load('w1');
    httpMock.expectOne('/api/work-items/w1/detail').flush('fail', { status: 404, statusText: 'Not Found' });
    await promise;
    expect(store.error()).toBeTruthy();
    expect(store.isPending()).toBe(false);
  });
});
