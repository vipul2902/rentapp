import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { StyleSheet, View } from 'react-native';

import { fieldErrorsFrom } from '@/api/errors';
import { register } from '@/auth/session';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { FormScreen } from '@/components/FormScreen';
import { InlineError } from '@/components/InlineError';
import { TextField } from '@/components/TextField';
import { spacing } from '@/theme/tokens';
import { email, hasErrors, lengthBetween, optionalPhone, password, required, validate } from '@/utils/validation';

const EMPTY = { organizationName: '', name: '', email: '', phone: '', password: '' };
type Values = typeof EMPTY;

export function RegisterScreen() {
  const [values, setValues] = useState<Values>(EMPTY);
  const [errors, setErrors] = useState<Partial<Record<keyof Values, string>>>({});

  const mutation = useMutation({
    mutationFn: () =>
      register({
        organizationName: values.organizationName.trim(),
        name: values.name.trim(),
        email: values.email.trim(),
        phone: values.phone.trim() || undefined,
        password: values.password,
      }),
    onError: (error) => setErrors(fieldErrorsFrom(error)),
  });

  const set = (field: keyof Values) => (text: string) => setValues((v) => ({ ...v, [field]: text }));

  const submit = () => {
    const found = validate(values, {
      organizationName: [required('Enter your PG or business name.'), lengthBetween(2, 200, 'Business name must be 2 to 200 characters.')],
      name: [required('Enter your name.'), lengthBetween(2, 120, 'Name must be 2 to 120 characters.')],
      email: [email],
      phone: [optionalPhone],
      password: [password],
    });
    setErrors(found);
    if (!hasErrors(found)) {
      mutation.mutate();
    }
  };

  return (
    <FormScreen>
      <View style={styles.header}>
        <AppText variant="title">Create your account</AppText>
        <AppText muted>You will be the owner. You can add staff later.</AppText>
      </View>

      <InlineError error={mutation.error} action="create your account" />

      <TextField label="PG / business name" value={values.organizationName} onChangeText={set('organizationName')} error={errors.organizationName} autoCapitalize="words" />
      <TextField label="Your name" value={values.name} onChangeText={set('name')} error={errors.name} autoCapitalize="words" autoComplete="name" />
      <TextField label="Email" value={values.email} onChangeText={set('email')} error={errors.email} keyboardType="email-address" autoCapitalize="none" autoComplete="email" />
      <TextField label="Phone (optional)" value={values.phone} onChangeText={set('phone')} error={errors.phone} keyboardType="phone-pad" autoComplete="tel" />
      <TextField
        label="Password"
        value={values.password}
        onChangeText={set('password')}
        error={errors.password}
        hint="At least 8 characters."
        secureTextEntry
        autoComplete="new-password"
        textContentType="newPassword"
      />

      <Button label="Create account" onPress={submit} loading={mutation.isPending} />
    </FormScreen>
  );
}

const styles = StyleSheet.create({
  header: { gap: spacing.sm },
});
