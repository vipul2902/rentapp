import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { Alert } from 'react-native';

import { authApi } from '@/api/auth';
import { fieldErrorsFrom } from '@/api/errors';
import { useCurrentUser } from '@/auth/SessionProvider';
import { signOut } from '@/auth/session';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Card } from '@/components/Card';
import { FormScreen } from '@/components/FormScreen';
import { InlineError } from '@/components/InlineError';
import { TextField } from '@/components/TextField';
import { useTheme } from '@/theme/useTheme';

/**
 * In-app account deletion (required by the App Store when an app lets people sign up). Needs the password.
 * Deleting the owner account closes the business for everyone.
 */
export function DeleteAccountScreen() {
  const { colors } = useTheme();
  const user = useCurrentUser();
  const isOwner = user.role === 'Owner';
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const remove = useMutation({ mutationFn: (pw: string) => authApi.deleteAccount(pw) });

  const submit = () => {
    if (!password) {
      setErrors({ password: 'Enter your password to confirm.' });
      return;
    }
    setErrors({});
    Alert.alert(
      isOwner ? `Close ${user.organization.name}?` : 'Delete your account?',
      'This cannot be undone.',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: isOwner ? 'Close and delete' : 'Delete account',
          style: 'destructive',
          onPress: () => remove.mutate(password, { onSuccess: () => void signOut(), onError: (e) => setErrors(fieldErrorsFrom(e)) }),
        },
      ],
    );
  };

  return (
    <FormScreen>
      <Card>
        <AppText variant="heading" color={colors.danger}>
          {isOwner ? 'This closes your whole business account' : 'This deletes your account'}
        </AppText>
        {isOwner ? (
          <>
            <AppText>• You and all your staff are signed out and can never sign in to {user.organization.name} again.</AppText>
            <AppText>• Your name, email and phone, and those of your staff, are erased.</AppText>
            <AppText>
              • Rent, payment and receipt records are financial records. They are kept as the law may require, but nobody can see them in the
              app any more.
            </AppText>
            <AppText>• You can sign up again with the same email, starting from scratch.</AppText>
          </>
        ) : (
          <>
            <AppText>• You are signed out everywhere and cannot sign in again.</AppText>
            <AppText>• Your name, email and phone are erased. Payments you recorded will show “Deleted user”.</AppText>
            <AppText>• {user.organization.name} and its records are not affected.</AppText>
          </>
        )}
      </Card>
      <InlineError error={remove.error && !errors.password ? remove.error : null} action="delete the account" />
      <TextField label="Password" value={password} onChangeText={setPassword} secureTextEntry autoComplete="current-password" error={errors.password} />
      <Button label={isOwner ? 'Close business and delete account' : 'Delete my account'} variant="danger" icon="trash-outline" onPress={submit} loading={remove.isPending} />
    </FormScreen>
  );
}
