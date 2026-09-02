import { computed } from '@angular/core';
import { signalStoreFeature, withState, withComputed } from '@ngrx/signals';

export type RequestStatus = 'idle' | 'pending' | 'fulfilled' | { error: string };

export function withRequestStatus() {
  return signalStoreFeature(
    withState<{ requestStatus: RequestStatus }>({ requestStatus: 'idle' }),
    withComputed(({ requestStatus }) => ({
      isPending: computed(() => requestStatus() === 'pending'),
      isFulfilled: computed(() => requestStatus() === 'fulfilled'),
      error: computed(() => {
        const s = requestStatus();
        return typeof s === 'object' && s !== null && 'error' in s ? (s as { error: string }).error : null;
      })
    }))
  );
}

export const setPending = () => ({ requestStatus: 'pending' as RequestStatus });
export const setFulfilled = () => ({ requestStatus: 'fulfilled' as RequestStatus });
export const setError = (e: string) => ({ requestStatus: { error: e } as RequestStatus });
