import { useMutation } from '@tanstack/react-query';
import { Link } from 'expo-router';
import { useRef, useState } from 'react';
import { StyleSheet, type TextInput, View } from 'react-native';

import { apiBaseUrl } from '@/api/config';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { FormScreen } from '@/components/FormScreen';
import { InlineError } from '@/components/InlineError';
import { TextField } from '@/components/TextField';
import { useSession } from '@/auth/SessionProvider';
import { signIn } from '@/auth/session';
import { spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';
import { email as emailRule, hasErrors, required, validate } from '@/utils/validation';

export function SignInScreen() {
  const { colors } = useTheme();
  const session = useSession();
  const passwordRef = useRef<TextInput>(null);
  const [values, setValues] = useState({ email: '', password: '' });
  const [errors, setErrors] = useState<Partial<Record<keyof typeof values, string>>>({});

  const mutation = useMutation({ mutationFn: () => signIn(values.email.trim(), values.password) });

  const submit = () => {
    const found = validate(values, { email: [emailRule], password: [required('Enter your password.')] });
    setErrors(found);
    if (!hasErrors(found)) {
      mutation.mutate();
    }
  };

  const expired = session.status === 'signedOut' && session.reason === 'expired';

  return (
    <FormScreen>
      <View style={styles.header}>
        <AppText variant="title">Sign in</AppText>
        <AppText muted>Know who has paid rent, who hasn&apos;t, and collect on time.</AppText>
      </View>

      {expired && !mutation.error ? (
        <AppText color={colors.warning} accessibilityRole="alert">
          Your session has ended. Please sign in again.
        </AppText>
      ) : null}
      <InlineError error={mutation.error} action="sign in" />

      <TextField
        label="Email"
        value={values.email}
        onChangeText={(email) => setValues((v) => ({ ...v, email }))}
        error={errors.email}
        keyboardType="email-address"
        autoCapitalize="none"
        autoComplete="email"
        textContentType="username"
        returnKeyType="next"
        onSubmitEditing={() => passwordRef.current?.focus()}
      />
      <TextField
        ref={passwordRef}
        label="Password"
        value={values.password}
        onChangeText={(password) => setValues((v) => ({ ...v, password }))}
        error={errors.password}
        secureTextEntry
        autoComplete="current-password"
        textContentType="password"
        returnKeyType="go"
        onSubmitEditing={submit}
      />

      <Button label="Sign in" onPress={submit} loading={mutation.isPending} />

      <View style={styles.links}>
        <Link href="/forgot-password" style={[styles.link, { color: colors.primary }]}>
          Forgot password?
        </Link>
        <Link href="/register" style={[styles.link, { color: colors.primary }]}>
          New here? Create an account
        </Link>
      </View>

      {__DEV__ ? (
        <AppText variant="caption" muted selectable style={styles.devServer}>
          Dev server: {apiBaseUrl ?? 'not configured'}
        </AppText>
      ) : null}
    </FormScreen>
  );
}

const styles = StyleSheet.create({
  header: { gap: spacing.sm, marginTop: spacing.xl },
  links: { gap: spacing.md, alignItems: 'center' },
  link: { fontSize: 16, paddingVertical: spacing.sm },
  devServer: { textAlign: 'center' },
});
