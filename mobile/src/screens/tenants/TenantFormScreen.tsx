import { router } from 'expo-router';
import { useState } from 'react';

import { fieldErrorsFrom } from '@/api/errors';
import type { TenantDetail, TenantDetailsInput } from '@/api/tenants';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Card } from '@/components/Card';
import { ErrorState } from '@/components/ErrorState';
import { FormScreen } from '@/components/FormScreen';
import { InlineError } from '@/components/InlineError';
import { LoadingState } from '@/components/LoadingState';
import { TextField } from '@/components/TextField';
import { ToggleRow } from '@/components/ToggleRow';
import { useCreateTenant, useTenant, useUpdateTenant } from '@/hooks/useTenants';
import { email as emailRule, hasErrors, lengthBetween, optionalPhone, required, validate, type Validator } from '@/utils/validation';

import { AssignBedFields, useAssignBedForm } from './AssignBedFields';

type Values = {
  fullName: string;
  phone: string;
  email: string;
  emergencyContactName: string;
  emergencyContactPhone: string;
  permanentAddress: string;
};

const optionalEmail: Validator = (v) => (v.trim() ? emailRule(v) : undefined);

const RULES = {
  fullName: [required("Enter the tenant's name."), lengthBetween(2, 120, 'Name must be 2 to 120 characters.')],
  phone: [required("Enter the tenant's phone number."), optionalPhone],
  email: [optionalEmail],
  emergencyContactPhone: [optionalPhone],
  permanentAddress: [(v: string) => (v.length <= 500 ? undefined : 'Address can be at most 500 characters.')],
};

const toInput = (v: Values): TenantDetailsInput => ({
  fullName: v.fullName.trim(),
  phone: v.phone.trim(),
  email: v.email.trim() || undefined,
  emergencyContactName: v.emergencyContactName.trim() || undefined,
  emergencyContactPhone: v.emergencyContactPhone.trim() || undefined,
  permanentAddress: v.permanentAddress.trim() || undefined,
});

function DetailsFields({ values, errors, set }: { values: Values; errors: Partial<Record<keyof Values, string>>; set: (f: keyof Values) => (t: string) => void }) {
  return (
    <>
      <TextField label="Full name" value={values.fullName} onChangeText={set('fullName')} error={errors.fullName} autoCapitalize="words" />
      <TextField label="Phone" value={values.phone} onChangeText={set('phone')} error={errors.phone} keyboardType="phone-pad" />
      <TextField label="Email (optional)" value={values.email} onChangeText={set('email')} error={errors.email} keyboardType="email-address" autoCapitalize="none" />
      <TextField label="Emergency contact name (optional)" value={values.emergencyContactName} onChangeText={set('emergencyContactName')} error={errors.emergencyContactName} autoCapitalize="words" />
      <TextField label="Emergency contact phone (optional)" value={values.emergencyContactPhone} onChangeText={set('emergencyContactPhone')} error={errors.emergencyContactPhone} keyboardType="phone-pad" />
      <TextField label="Permanent address (optional)" value={values.permanentAddress} onChangeText={set('permanentAddress')} error={errors.permanentAddress} multiline />
    </>
  );
}

function useDetails(initial?: TenantDetail) {
  const [values, setValues] = useState<Values>({
    fullName: initial?.fullName ?? '',
    phone: initial?.phone ?? '',
    email: initial?.email ?? '',
    emergencyContactName: initial?.emergencyContactName ?? '',
    emergencyContactPhone: initial?.emergencyContactPhone ?? '',
    permanentAddress: initial?.permanentAddress ?? '',
  });
  const [errors, setErrors] = useState<Partial<Record<keyof Values, string>>>({});
  const set = (field: keyof Values) => (text: string) => setValues((v) => ({ ...v, [field]: text }));
  return { values, errors, setErrors, set };
}

/** Add a tenant; optionally assign a bed in the same step. `bedId` pre-selects a bed (from a room screen). */
export function CreateTenantScreen({ bedId }: { bedId?: string }) {
  const details = useDetails();
  const [assign, setAssign] = useState(true);
  const bed = useAssignBedForm(bedId);
  const create = useCreateTenant();

  const submit = () => {
    const found = validate(details.values, RULES);
    details.setErrors(found);
    const moveIn = assign ? bed.validate() : undefined;
    if (hasErrors(found) || moveIn === null) return;
    create.mutate(
      { ...toInput(details.values), moveIn },
      {
        onSuccess: (tenant) => router.replace({ pathname: '/tenants/[id]', params: { id: tenant.id } }),
        onError: (error) => {
          const fields = fieldErrorsFrom(error);
          details.setErrors(fields);
          bed.setErrors(Object.fromEntries(Object.entries(fields).map(([k, v]) => [k.replace(/^moveIn\./, ''), v])));
        },
      },
    );
  };

  return (
    <FormScreen>
      <InlineError error={create.error} action="add the tenant" />
      <DetailsFields {...details} />
      <Card>
        <ToggleRow label="Assign a bed now" description="You can also do this later from the tenant's page." value={assign} onChange={setAssign} />
        {assign ? <AssignBedFields form={bed} /> : null}
      </Card>
      <AppText variant="caption" muted>
        Do not store ID documents (Aadhaar, PAN) here. Only contact details are kept.
      </AppText>
      <Button label="Add tenant" onPress={submit} loading={create.isPending} />
    </FormScreen>
  );
}

export function EditTenantScreen({ tenantId }: { tenantId: string }) {
  const query = useTenant(tenantId);
  if (query.isPending) return <LoadingState />;
  if (query.error) {
    return (
      <FormScreen>
        <ErrorState error={query.error} action="load this tenant" onRetry={() => void query.refetch()} />
      </FormScreen>
    );
  }
  return <EditTenantForm tenant={query.data} />;
}

function EditTenantForm({ tenant }: { tenant: TenantDetail }) {
  const details = useDetails(tenant);
  const update = useUpdateTenant(tenant.id);

  const submit = () => {
    const found = validate(details.values, RULES);
    details.setErrors(found);
    if (hasErrors(found)) return;
    update.mutate(toInput(details.values), {
      onSuccess: () => router.back(),
      onError: (error) => details.setErrors(fieldErrorsFrom(error)),
    });
  };

  return (
    <FormScreen>
      <InlineError error={update.error} action="save the tenant" />
      <DetailsFields {...details} />
      <Button label="Save changes" onPress={submit} loading={update.isPending} />
    </FormScreen>
  );
}
