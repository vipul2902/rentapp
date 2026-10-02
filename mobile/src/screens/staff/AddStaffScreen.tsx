import { router } from 'expo-router';
import { useState } from 'react';

import { fieldErrorsFrom } from '@/api/errors';
import type { StaffPermission } from '@/api/types';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Card } from '@/components/Card';
import { FormScreen } from '@/components/FormScreen';
import { InlineError } from '@/components/InlineError';
import { PermissionToggles } from '@/components/PermissionToggles';
import { TextField } from '@/components/TextField';
import { useCreateStaff } from '@/hooks/useStaff';
import { email, hasErrors, lengthBetween, optionalPhone, password, required, validate } from '@/utils/validation';

/** A sensible default for a PG manager: see tenants, record payments, issue receipts, send reminders. */
const DEFAULT_PERMISSIONS: StaffPermission[] = ['ViewProperties', 'ViewTenants', 'RecordPayments', 'GenerateReceipts', 'SendReminders'];

const EMPTY = { name: '', email: '', phone: '', password: '' };
type Values = typeof EMPTY;

export function AddStaffScreen() {
  const [values, setValues] = useState<Values>(EMPTY);
  const [permissions, setPermissions] = useState<StaffPermission[]>(DEFAULT_PERMISSIONS);
  const [errors, setErrors] = useState<Partial<Record<keyof Values, string>>>({});
  const create = useCreateStaff();

  const set = (field: keyof Values) => (text: string) => setValues((v) => ({ ...v, [field]: text }));

  const submit = () => {
    const found = validate(values, {
      name: [required("Enter the staff member's name."), lengthBetween(2, 120, 'Name must be 2 to 120 characters.')],
      email: [email],
      phone: [optionalPhone],
      password: [password],
    });
    setErrors(found);
    if (hasErrors(found)) {
      return;
    }
    create.mutate(
      {
        name: values.name.trim(),
        email: values.email.trim(),
        phone: values.phone.trim() || undefined,
        password: values.password,
        permissions,
      },
      { onSuccess: () => router.back(), onError: (error) => setErrors(fieldErrorsFrom(error)) },
    );
  };

  return (
    <FormScreen>
      <InlineError error={create.error} action="add this staff member" />

      <TextField label="Name" value={values.name} onChangeText={set('name')} error={errors.name} autoCapitalize="words" />
      <TextField label="Email (used to sign in)" value={values.email} onChangeText={set('email')} error={errors.email} keyboardType="email-address" autoCapitalize="none" />
      <TextField label="Phone (optional)" value={values.phone} onChangeText={set('phone')} error={errors.phone} keyboardType="phone-pad" />
      <TextField
        label="Temporary password"
        value={values.password}
        onChangeText={set('password')}
        error={errors.password}
        hint="Share this with them privately. At least 8 characters."
        secureTextEntry
        autoComplete="new-password"
      />

      <Card>
        <AppText variant="heading">What can they do?</AppText>
        <PermissionToggles value={permissions} onChange={setPermissions} />
      </Card>

      <Button label="Add staff" onPress={submit} loading={create.isPending} />
    </FormScreen>
  );
}
