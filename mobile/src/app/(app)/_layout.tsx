import { Stack } from 'expo-router';

import { can } from '@/auth/permissions';
import { useCurrentUser } from '@/auth/SessionProvider';

/** Route guards are a UI convenience only; the API enforces every permission. */
export default function AppLayout() {
  const user = useCurrentUser();
  const isOwner = user.role === 'Owner';
  const canViewProperties = can(user, 'ViewProperties');

  return (
    <Stack>
      <Stack.Screen name="index" options={{ title: 'Home' }} />
      <Stack.Screen name="system-status" options={{ title: 'System status' }} />

      <Stack.Protected guard={canViewProperties}>
        <Stack.Screen name="properties/index" options={{ title: 'Properties' }} />
        <Stack.Screen name="properties/[id]/index" options={{ title: 'Property' }} />
        <Stack.Screen name="rooms/[id]/index" options={{ title: 'Room' }} />
      </Stack.Protected>

      <Stack.Protected guard={isOwner}>
        <Stack.Screen name="properties/new" options={{ title: 'Add property', presentation: 'modal' }} />
        <Stack.Screen name="properties/[id]/edit" options={{ title: 'Edit property', presentation: 'modal' }} />
        <Stack.Screen name="properties/[id]/rooms/new" options={{ title: 'Add room', presentation: 'modal' }} />
        <Stack.Screen name="rooms/[id]/edit" options={{ title: 'Edit room', presentation: 'modal' }} />
        <Stack.Screen name="rooms/[id]/beds/new" options={{ title: 'Add bed', presentation: 'modal' }} />
        <Stack.Screen name="beds/[id]/edit" options={{ title: 'Edit bed', presentation: 'modal' }} />

        <Stack.Screen name="staff/index" options={{ title: 'Staff' }} />
        <Stack.Screen name="staff/new" options={{ title: 'Add staff', presentation: 'modal' }} />
        <Stack.Screen name="staff/[id]" options={{ title: 'Staff member' }} />
      </Stack.Protected>
    </Stack>
  );
}
