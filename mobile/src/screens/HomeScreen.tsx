import { router } from 'expo-router';
import { Alert, StyleSheet, View } from 'react-native';

import { can } from '@/auth/permissions';
import { useCurrentUser } from '@/auth/SessionProvider';
import { signOut } from '@/auth/session';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Card } from '@/components/Card';
import { FormScreen } from '@/components/FormScreen';
import { ListRow } from '@/components/ListRow';
import { StatusPill } from '@/components/StatusPill';
import { radius } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

/** Placeholder home until the dashboard arrives in Phase 7. */
export function HomeScreen() {
  const user = useCurrentUser();
  const { colors } = useTheme();
  const isOwner = user.role === 'Owner';

  const confirmSignOut = () =>
    Alert.alert('Sign out?', 'You will need your email and password to sign in again.', [
      { text: 'Cancel', style: 'cancel' },
      { text: 'Sign out', style: 'destructive', onPress: () => void signOut() },
    ]);

  return (
    <FormScreen>
      <Card>
        <AppText variant="title">Hi, {user.name.split(' ')[0]}</AppText>
        <AppText muted>{user.organization.name}</AppText>
        <StatusPill label={isOwner ? 'Owner' : 'Staff'} tone="info" />
      </Card>

      <Card>
        <AppText variant="heading">Coming next</AppText>
        <AppText muted>Monthly rent dues, then payments and receipts.</AppText>
      </Card>

      <View style={[styles.menu, { borderColor: colors.border }]}>
        {can(user, 'ViewTenants') ? (
          <ListRow title="Tenants" subtitle="Who lives where, move-ins and move-outs" onPress={() => router.push('/tenants')} />
        ) : null}
        {can(user, 'ViewProperties') ? (
          <ListRow title="Properties" subtitle="PGs, rooms, beds and vacancies" onPress={() => router.push('/properties')} />
        ) : null}
        {isOwner ? (
          <ListRow title="Staff" subtitle="Add staff and choose what they can do" onPress={() => router.push('/staff')} />
        ) : null}
        <ListRow title="System status" subtitle="Check the connection to the server" onPress={() => router.push('/system-status')} />
      </View>

      <Button label="Sign out" variant="secondary" onPress={confirmSignOut} />
    </FormScreen>
  );
}

const styles = StyleSheet.create({
  menu: { borderRadius: radius.lg, borderWidth: StyleSheet.hairlineWidth, overflow: 'hidden', gap: StyleSheet.hairlineWidth },
});
