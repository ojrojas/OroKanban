import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { KanbanBoardStore } from './kanban-board.store';

describe('KanbanBoardStore', () => {
  let store: InstanceType<typeof KanbanBoardStore>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [KanbanBoardStore, provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(KanbanBoardStore);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should have initial idle state', () => {
    expect(store.columns()).toEqual([]);
    expect(store.requestStatus()).toBe('idle');
    expect(store.isPending()).toBe(false);
    expect(store.isFulfilled()).toBe(false);
    expect(store.error()).toBeNull();
    expect(store.overdueCount()).toBe(0);
    expect(store.totalCount()).toBe(0);
  });

  it('loadBoard → pending → fulfilled with columns', async () => {
    store.setProject('proj-1');
    const promise = store.loadBoard();

    expect(store.isPending()).toBe(true);
    expect(store.requestStatus()).toBe('pending');

    const req = httpMock.expectOne('/api/projects/proj-1/board');
    expect(req.request.method).toBe('GET');
    req.flush({
      columns: [
        { status: 'Backlog', statusId: 1, count: 1, items: [{ id: 'w1', title: 'T1', criticality: 'High', isOverdue: true }] },
        { status: 'Planned', statusId: 2, count: 0, items: [] },
      ],
    });

    await promise;

    expect(store.isFulfilled()).toBe(true);
    expect(store.error()).toBeNull();
    expect(store.columns().length).toBe(2);
    expect(store.overdueCount()).toBe(1);
    expect(store.totalCount()).toBe(1);
  });

  it('loadBoard → error on HTTP failure', async () => {
    store.setProject('proj-1');
    const promise = store.loadBoard();
    const req = httpMock.expectOne('/api/projects/proj-1/board');
    req.flush('fail', { status: 500, statusText: 'Server Error' });
    await promise;
    expect(store.isPending()).toBe(false);
    expect(store.error()).toBeTruthy();
    expect(store.isFulfilled()).toBe(false);
  });

  it('dragDrop → pending → reload fulfilled', async () => {
    store.setProject('proj-1');
    const promise = store.dragDrop('w1', 'Planned', 1);
    expect(store.isPending()).toBe(true);

    const post = httpMock.expectOne('/api/workitems/w1/status');
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual({ targetStatus: 'Planned', expectedVersion: 1 });
    post.flush({});
    // let the await http.post().toPromise() continuation fire and trigger GET
    await Promise.resolve();

    const get = httpMock.expectOne('/api/projects/proj-1/board');
    get.flush({ columns: [{ status: 'Planned', statusId: 2, count: 1, items: [{ id: 'w1', isOverdue: false }] }] });

    await promise;
    expect(store.isFulfilled()).toBe(true);
    expect(store.columns()[0].items[0].id).toBe('w1');
  });
});
