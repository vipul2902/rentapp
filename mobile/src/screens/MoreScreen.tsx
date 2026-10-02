import { router } from 'expo-router';
import { Alert, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { can } from '@/auth/permissions';
import { useCurrentUser } from '@/auth/SessionProvider';
import { signOut } from '@/auth/session';
import { AppText } from '@/components/AppText';
import { Avatar } from '@/components/Avatar';
import { Button } from '@/components/Button';
import { Card } from '@/components/Card';
import { ListRow } from '@/components/ListRow';
import { Logo } from '@/components/Logo';
import { StatusPill } from '@/components/StatusPill';
import { elevation, radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

export function MoreScreen() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const user = useCurrentUser();
  const isOwner = user.role === 'Owner';

  const confirmSignOut = () =>
    Alert.alert('Sign out?', 'You will need your email and password to sign in again.', [
      { text: 'Cancel', style: 'cancel' },
      { text: 'Sign out', style: 'destructive', onPress: () => void signOut() },
    ]);

  return (
    <ScrollView
      style={{ backgroundColor: colors.background }}
      contentContainerStyle={[styles.content, { paddingTop: insets.top + spacing.lg, paddingBottom: insets.bottom + spacing.xl }]}
    >
      <Logo size={48} tagline="Rent collection made simple" />

      <Card>
        <View style={styles.profile}>
          <Avatar name={user.name} size={52} />
          <View style={styles.flex}>
            <AppText variant="heading">{user.name}</AppText>
            <AppText muted>{user.email}</AppText>
            <AppText muted>{user.organization.name}</AppText>
          </View>
          <StatusPill label={isOwner ? 'Owner' : 'Staff'} tone="info" icon={isOwner ? 'star' : 'person'} />
        </View>
      </Card>

      <View style={[styles.list, { backgroundColor: colors.surface }, elevation(colors.shadow)]}>
        {can(user, 'SendReminders') ? (
          <ListRow icon="notifications-outline" title="Reminders" subtitle="Who to remind today, and what was sent" onPress={() => router.push('/reminders')} />
        ) : null}
        {isOwner ? (
          <ListRow icon="people-outline" title="Staff" subtitle="Add staff and choose what they can do" onPress={() => router.push('/staff')} />
        ) : null}
        <ListRow icon="pulse-outline" title="System status" subtitle="Check the connection to the server" onPress={() => router.push('/system-status')} />
      </View>

      <View style={[styles.list, { backgroundColor: colors.surface }, elevation(colors.shadow)]}>
        <ListRow
          icon="trash-outline"
          title="Delete account"
          subtitle={isOwner ? 'Close your business account for good' : 'Erase your account for good'}
          onPress={() => router.push('/account/delete')}
        />
      </View>

      <Button label="Sign out" icon="log-out-outline" variant="danger" onPress={confirmSignOut} />
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  content: { paddingHorizontal: spacing.lg, gap: spacing.lg },
  flex: { flex: 1 },
  profile: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  list: { borderRadius: radius.lg, overflow: 'hidden' },
});
