import { Stack } from 'expo-router';

import { useCurrentUser } from '@/auth/SessionProvider';

export default function AppLayout() {
  const user = useCurrentUser();

  return (
    <Stack>
      <Stack.Screen name="index" options={{ title: 'Home' }} />
      <Stack.Screen name="system-status" options={{ title: 'System status' }} />
      {/* UI guard only; the API enforces owner-only access on every staff endpoint. */}
      <Stack.Protected guard={user.role === 'Owner'}>
        <Stack.Screen name="staff/index" options={{ title: 'Staff' }} />
        <Stack.Screen name="staff/new" options={{ title: 'Add staff', presentation: 'modal' }} />
        <Stack.Screen name="staff/[id]" options={{ title: 'Staff member' }} />
      </Stack.Protected>
    </Stack>
  );
}
