import { Stack } from 'expo-router';

import { can } from '@/auth/permissions';
import { useCurrentUser } from '@/auth/SessionProvider';
import { useTheme } from '@/theme/useTheme';

/** Route guards are a UI convenience only; the API enforces every permission. */
export default function AppLayout() {
  const { colors } = useTheme();
  const user = useCurrentUser();
  const isOwner = user.role === 'Owner';
  const canViewProperties = can(user, 'ViewProperties');
  const canViewTenants = can(user, 'ViewTenants');
  // Recording starts from a tenant or a due, so it needs both permissions.
  const canRecordPayments = canViewTenants && can(user, 'RecordPayments');
  const canSeeReceipts = can(user, 'RecordPayments') || can(user, 'GenerateReceipts');
  const canRemind = can(user, 'SendReminders');

  return (
    <Stack
      screenOptions={{
        headerStyle: { backgroundColor: colors.background },
        headerShadowVisible: false,
        headerTintColor: colors.primary,
        headerTitleStyle: { fontWeight: '700', color: colors.text },
        contentStyle: { backgroundColor: colors.background },
      }}
    >
      <Stack.Screen name="(tabs)" options={{ headerShown: false, title: 'Home' }} />
      <Stack.Screen name="system-status" options={{ title: 'System status' }} />
      <Stack.Screen name="account/delete" options={{ title: 'Delete account', presentation: 'modal' }} />

      <Stack.Protected guard={canViewProperties}>
        <Stack.Screen name="properties/[id]/index" options={{ title: 'Property' }} />
        <Stack.Screen name="rooms/[id]/index" options={{ title: 'Room' }} />
      </Stack.Protected>

      <Stack.Protected guard={canViewTenants}>
        <Stack.Screen name="tenants/[id]/index" options={{ title: 'Tenant' }} />
        <Stack.Screen name="rent/[id]/index" options={{ title: 'Rent due' }} />
        <Stack.Screen name="payments/[id]/index" options={{ title: 'Payment' }} />
      </Stack.Protected>

      <Stack.Protected guard={canRecordPayments}>
        <Stack.Screen name="payments/new" options={{ title: 'Record payment', presentation: 'modal' }} />
      </Stack.Protected>

      <Stack.Protected guard={canRemind}>
        <Stack.Screen name="reminders/index" options={{ title: 'Reminders' }} />
        <Stack.Screen name="reminders/new" options={{ title: 'Send reminder', presentation: 'modal' }} />
      </Stack.Protected>

      <Stack.Protected guard={canSeeReceipts}>
        <Stack.Screen name="receipts/[id]" options={{ title: 'Receipt' }} />
      </Stack.Protected>

      <Stack.Protected guard={isOwner}>
        <Stack.Screen name="rent/[id]/waive" options={{ title: 'Waive an amount', presentation: 'modal' }} />
        <Stack.Screen name="payments/[id]/void" options={{ title: 'Void payment', presentation: 'modal' }} />

        <Stack.Screen name="tenants/new" options={{ title: 'Add tenant', presentation: 'modal' }} />
        <Stack.Screen name="tenants/[id]/edit" options={{ title: 'Edit tenant', presentation: 'modal' }} />
        <Stack.Screen name="tenants/[id]/move-in" options={{ title: 'Assign a bed', presentation: 'modal' }} />
        <Stack.Screen name="tenants/[id]/move-out" options={{ title: 'Move out', presentation: 'modal' }} />
        <Stack.Screen name="tenants/[id]/move" options={{ title: 'Move to another bed', presentation: 'modal' }} />
        <Stack.Screen name="tenants/[id]/terms" options={{ title: 'Rent and deposit', presentation: 'modal' }} />

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
