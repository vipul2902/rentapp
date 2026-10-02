import { useQueryClient } from '@tanstack/react-query';
import { type PropsWithChildren, useEffect, useSyncExternalStore } from 'react';

import type { UserProfile } from '@/api/types';

import { restoreSession, sessionStore, type SessionState } from './session';

export function useSession(): SessionState {
  return useSyncExternalStore(sessionStore.subscribe, sessionStore.getState);
}

/** The signed-in user. Only call inside screens that are reachable when signed in. */
export function useCurrentUser(): UserProfile {
  const session = useSession();
  if (session.status !== 'signedIn') {
    throw new Error('useCurrentUser called while signed out.');
  }
  return session.user;
}

/** Restores the saved session on start and drops all cached server data when the user signs out. */
export function SessionProvider({ children }: PropsWithChildren) {
  const queryClient = useQueryClient();

  useEffect(() => {
    void restoreSession();
  }, []);

  useEffect(
    () =>
      sessionStore.subscribe(() => {
        if (sessionStore.getState().status === 'signedOut') {
          queryClient.clear();
        }
      }),
    [queryClient],
  );

  return children;
}
