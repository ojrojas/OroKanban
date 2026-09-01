import { computed } from '@angular/core';
import { signalStoreFeature, withComputed, withState } from '@ngrx/signals';

export type RequestStatus = 'idle' | 'pending' | 'fulfilled' | { error: string };
export type RequestStatusState = { requestStatus: RequestStatus };

export function withRequestStatus() {
  return signalStoreFeature(
    withState<RequestStatusState>({ requestStatus: 'idle' }),
    withComputed(({ requestStatus }) => ({
      isPending: computed(() => requestStatus() === 'pending'),
      isFulfilled: computed(() => requestStatus() === 'fulfilled'),
      error: computed(() => {
        const s = requestStatus();
        return typeof s === 'object' ? (s as { error: string }).error : null;
      }),
    }))
  );
}

export const setPending = (): RequestStatusState => ({ requestStatus: 'pending' });
export const setFulfilled = (): RequestStatusState => ({ requestStatus: 'fulfilled' });
export const setError = (error: string): RequestStatusState => ({ requestStatus: { error } });
