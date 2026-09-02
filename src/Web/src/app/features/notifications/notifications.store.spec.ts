import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { NotificationsStore } from './notifications.store';

describe('NotificationsStore', () => {
  let store: InstanceType<typeof NotificationsStore>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [NotificationsStore, provideHttpClient(), provideHttpClientTesting()] });
    store = TestBed.inject(NotificationsStore);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('should compute unreadCount from entities', async () => {
    const promise = store.load();
    const req = httpMock.expectOne('/api/notifications?page=1&pageSize=20');
    req.flush({ items: [{ id: '1', title: 'A', readAt: null }, { id: '2', title: 'B', readAt: '2026-01-01' }, { id: '3', title: 'C', readAt: null }] });
    await promise;
    expect(store.entities().length).toBe(3);
    expect(store.unreadCount()).toBe(2);
    expect(store.total()).toBe(3);
  });

  it('markRead → pending → fulfilled and decrements unreadCount', async () => {
    // preload
    let promise = store.load();
    httpMock.expectOne('/api/notifications?page=1&pageSize=20').flush({ items: [{ id: '1', readAt: null, title: 'A' }] });
    await promise;
    expect(store.unreadCount()).toBe(1);

    const markPromise = store.markRead('1');
    expect(store.isPending()).toBe(true);
    const req = httpMock.expectOne('/api/notifications/1/read');
    expect(req.request.method).toBe('POST');
    req.flush({});
    await markPromise;
    expect(store.isFulfilled()).toBe(true);
    expect(store.unreadCount()).toBe(0);
  });

  it('load → error sets error signal', async () => {
    const promise = store.load();
    httpMock.expectOne('/api/notifications?page=1&pageSize=20').flush('fail', { status: 500, statusText: 'Error' });
    await promise;
    expect(store.error()).toBeTruthy();
    expect(store.isPending()).toBe(false);
  });
});
