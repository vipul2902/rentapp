import { QueryClientProvider } from '@tanstack/react-query';
import { DarkTheme, DefaultTheme, Stack, ThemeProvider } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useState } from 'react';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import { createQueryClient } from '@/api/queryClient';
import { useTheme } from '@/theme/useTheme';

export default function RootLayout() {
  const [queryClient] = useState(createQueryClient);
  const { isDark } = useTheme();

  return (
    <SafeAreaProvider>
      <QueryClientProvider client={queryClient}>
        <ThemeProvider value={isDark ? DarkTheme : DefaultTheme}>
          <Stack>
            <Stack.Screen name="index" options={{ title: 'System status' }} />
          </Stack>
          <StatusBar style="auto" />
        </ThemeProvider>
      </QueryClientProvider>
    </SafeAreaProvider>
  );
}
