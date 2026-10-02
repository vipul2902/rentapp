import { QueryClientProvider } from '@tanstack/react-query';
import { DarkTheme, DefaultTheme, Stack, ThemeProvider } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useState } from 'react';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import { createQueryClient } from '@/api/queryClient';
import { restoreSession } from '@/auth/session';
import { SessionProvider, useSession } from '@/auth/SessionProvider';
import { ConnectionErrorScreen } from '@/screens/ConnectionErrorScreen';
import { useTheme } from '@/theme/useTheme';

// Keep the splash up until we know whether a saved session can be resumed.
void SplashScreen.preventAutoHideAsync();

export default function RootLayout() {
  const [queryClient] = useState(createQueryClient);
  const { isDark } = useTheme();

  return (
    <SafeAreaProvider>
      <QueryClientProvider client={queryClient}>
        <SessionProvider>
          <ThemeProvider value={isDark ? DarkTheme : DefaultTheme}>
            <RootNavigator />
            <StatusBar style="auto" />
          </ThemeProvider>
        </SessionProvider>
      </QueryClientProvider>
    </SafeAreaProvider>
  );
}

function RootNavigator() {
  const session = useSession();
  const restoring = session.status === 'restoring';

  useEffect(() => {
    if (!restoring) {
      void SplashScreen.hideAsync();
    }
  }, [restoring]);

  if (session.status === 'unavailable') {
    return <ConnectionErrorScreen onRetry={restoreSession} />;
  }

  const signedIn = session.status === 'signedIn';
  return (
    <Stack screenOptions={{ headerShown: false }}>
      <Stack.Protected guard={signedIn}>
        <Stack.Screen name="(app)" />
      </Stack.Protected>
      <Stack.Protected guard={!signedIn}>
        <Stack.Screen name="(auth)" />
      </Stack.Protected>
    </Stack>
  );
}
