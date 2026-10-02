import { useState } from 'react';
import { Alert, RefreshControl } from 'react-native';

import type { StaffMember, StaffPermission } from '@/api/types';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Card } from '@/components/Card';
import { ErrorState } from '@/components/ErrorState';
import { FormScreen } from '@/components/FormScreen';
import { InlineError } from '@/components/InlineError';
import { LoadingState } from '@/components/LoadingState';
import { PermissionToggles } from '@/components/PermissionToggles';
import { StatusPill } from '@/components/StatusPill';
import { TextField } from '@/components/TextField';
import { useResetStaffPassword, useSetStaffActive, useStaffMember, useUpdatePermissions } from '@/hooks/useStaff';
import { password as passwordRule } from '@/utils/validation';

export function StaffDetailScreen({ id }: { id: string }) {
  const query = useStaffMember(id);

  if (query.isPending) {
    return <LoadingState message="Loading…" />;
  }
  if (query.error) {
    return (
      <FormScreen>
        <ErrorState error={query.error} action="load this staff member" onRetry={() => void query.refetch()} retrying={query.isFetching} />
      </FormScreen>
    );
  }

  // Keyed so local edits reset when the saved record changes.
  return <StaffDetail key={`${query.data.id}:${query.data.permissions.join()}`} member={query.data} refetch={query.refetch} refreshing={query.isRefetching} />;
}

function StaffDetail({ member, refetch, refreshing }: { member: StaffMember; refetch: () => unknown; refreshing: boolean }) {
  const [permissions, setPermissions] = useState<StaffPermission[]>(member.permissions);
  const [newPassword, setNewPassword] = useState('');
  const [passwordError, setPasswordError] = useState<string>();
  const [passwordSaved, setPasswordSaved] = useState(false);

  const update = useUpdatePermissions(member.id);
  const setActive = useSetStaffActive(member.id);
  const reset = useResetStaffPassword(member.id);

  const active = member.status === 'Active';
  const changed = permissions.join() !== member.permissions.join();

  const toggleActive = () =>
    Alert.alert(
      active ? `Disable ${member.name}?` : `Enable ${member.name}?`,
      active ? 'They will be signed out on all devices and cannot sign in until you enable them again.' : 'They will be able to sign in again.',
      [
        { text: 'Cancel', style: 'cancel' },
        { text: active ? 'Disable' : 'Enable', style: active ? 'destructive' : 'default', onPress: () => setActive.mutate(!active) },
      ],
    );

  const resetPassword = () => {
    const error = passwordRule(newPassword);
    setPasswordError(error);
    setPasswordSaved(false);
    if (!error) {
      reset.mutate(newPassword, {
        onSuccess: () => {
          setNewPassword('');
          setPasswordSaved(true);
        },
      });
    }
  };

  return (
    <FormScreen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refetch()} />}>
      <Card>
        <AppText variant="title">{member.name}</AppText>
        <AppText muted selectable>
          {member.email}
          {member.phone ? ` · ${member.phone}` : ''}
        </AppText>
        <StatusPill label={active ? 'Active' : 'Disabled'} tone={active ? 'success' : 'neutral'} />
        <AppText variant="caption" muted>
          {member.lastLoginAt ? `Last signed in ${new Date(member.lastLoginAt).toLocaleString()}` : 'Has not signed in yet'}
        </AppText>
      </Card>

      <Card>
        <AppText variant="heading">Permissions</AppText>
        <PermissionToggles value={permissions} onChange={setPermissions} disabled={update.isPending} />
        <InlineError error={update.error} action="save permissions" />
        <Button label="Save permissions" onPress={() => update.mutate(permissions)} loading={update.isPending} disabled={!changed} />
        <AppText variant="caption" muted>
          Changes apply within 15 minutes on their phone.
        </AppText>
      </Card>

      <Card>
        <AppText variant="heading">Reset password</AppText>
        <TextField
          label="New password"
          value={newPassword}
          onChangeText={setNewPassword}
          error={passwordError}
          hint="They will be signed out and must use this password."
          secureTextEntry
          autoComplete="new-password"
        />
        <InlineError error={reset.error} action="reset the password" />
        {passwordSaved ? <AppText accessibilityRole="alert">Password updated. Share it with them privately.</AppText> : null}
        <Button label="Set new password" variant="secondary" onPress={resetPassword} loading={reset.isPending} />
      </Card>

      <InlineError error={setActive.error} action={active ? 'disable this account' : 'enable this account'} />
      <Button label={active ? 'Disable account' : 'Enable account'} variant="secondary" onPress={toggleActive} loading={setActive.isPending} />
    </FormScreen>
  );
}
