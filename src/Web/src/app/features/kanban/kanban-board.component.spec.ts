import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { KanbanBoardComponent } from './kanban-board.component';
import { KanbanBoardStore } from './kanban-board.store';
import { patchState } from '@ngrx/signals';
import { setError, setFulfilled, setPending } from './with-request-status';

describe('KanbanBoardComponent', () => {
  let store: InstanceType<typeof KanbanBoardStore>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [KanbanBoardComponent],
      providers: [KanbanBoardStore, provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    store = TestBed.inject(KanbanBoardStore);
  });

  it('should show loading when isPending', async () => {
    patchState(store as any, setPending());
    const fixture = TestBed.createComponent(KanbanBoardComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.kanban-loading')?.textContent).toContain('Cargando');
  });

  it('should show error banner when error()', async () => {
    patchState(store as any, setError('load failed'));
    const fixture = TestBed.createComponent(KanbanBoardComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.kanban-error')?.textContent).toContain('load failed');
  });

  it('should render columns and empty-state when fulfilled', async () => {
    patchState(store as any, { columns: [
      { status: 'Backlog', statusId: 1, count: 0, items: [] },
      { status: 'Planned', statusId: 2, count: 1, items: [{ id: 'w1', title: 'Task 1', criticality: 'High', isOverdue: true, version: 1 }] },
    ]}, setFulfilled());
    const fixture = TestBed.createComponent(KanbanBoardComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Backlog');
    expect(fixture.nativeElement.querySelector('.badge.overdue')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('Sin elementos'); // empty column + isFulfilled
  });
});
