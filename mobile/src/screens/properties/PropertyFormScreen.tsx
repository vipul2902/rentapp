import { router } from 'expo-router';
import { useState } from 'react';

import { fieldErrorsFrom } from '@/api/errors';
import type { Property, PropertyInput } from '@/api/properties';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { FormScreen } from '@/components/FormScreen';
import { InlineError } from '@/components/InlineError';
import { LoadingState } from '@/components/LoadingState';
import { TextField } from '@/components/TextField';
import { useCreateProperty, useProperty, useUpdateProperty } from '@/hooks/useProperties';
import { hasErrors, lengthBetween, optionalPhone, required, validate, type Validator } from '@/utils/validation';

type Values = { name: string; address: string; city: string; state: string; postalCode: string; contactPhone: string };

const optionalPostalCode: Validator = (v) => (!v.trim() || /^[A-Za-z0-9 -]{3,12}$/.test(v.trim()) ? undefined : 'Enter a valid postal code.');

const RULES = {
  name: [required('Enter the property name.'), lengthBetween(2, 200, 'Name must be 2 to 200 characters.')],
  address: [required('Enter the address.'), lengthBetween(1, 300, 'Address can be at most 300 characters.')],
  city: [required('Enter the city.'), lengthBetween(1, 100, 'City can be at most 100 characters.')],
  postalCode: [optionalPostalCode],
  contactPhone: [optionalPhone],
};

function toInput(values: Values): PropertyInput {
  return {
    name: values.name.trim(),
    address: values.address.trim(),
    city: values.city.trim(),
    state: values.state.trim() || undefined,
    postalCode: values.postalCode.trim() || undefined,
    contactPhone: values.contactPhone.trim() || undefined,
  };
}

/** Create when `propertyId` is absent, edit otherwise. */
export function PropertyFormScreen({ propertyId }: { propertyId?: string }) {
  if (!propertyId) {
    return <PropertyForm />;
  }
  return <EditProperty propertyId={propertyId} />;
}

function EditProperty({ propertyId }: { propertyId: string }) {
  const query = useProperty(propertyId);
  if (query.isPending) return <LoadingState />;
  if (query.error) {
    return (
      <FormScreen>
        <ErrorState error={query.error} action="load this property" onRetry={() => void query.refetch()} />
      </FormScreen>
    );
  }
  return <PropertyForm existing={query.data} />;
}

function PropertyForm({ existing }: { existing?: Property }) {
  const [values, setValues] = useState<Values>({
    name: existing?.name ?? '',
    address: existing?.address ?? '',
    city: existing?.city ?? '',
    state: existing?.state ?? '',
    postalCode: existing?.postalCode ?? '',
    contactPhone: existing?.contactPhone ?? '',
  });
  const [errors, setErrors] = useState<Partial<Record<keyof Values, string>>>({});
  const create = useCreateProperty();
  const update = useUpdateProperty(existing?.id ?? '');
  const mutation = existing ? update : create;

  const set = (field: keyof Values) => (text: string) => setValues((v) => ({ ...v, [field]: text }));

  const submit = () => {
    const found = validate(values, RULES);
    setErrors(found);
    if (hasErrors(found)) return;
    mutation.mutate(toInput(values), {
      onSuccess: (property) =>
        existing ? router.back() : router.replace({ pathname: '/properties/[id]', params: { id: property.id } }),
      onError: (error) => setErrors(fieldErrorsFrom(error)),
    });
  };

  return (
    <FormScreen>
      <InlineError error={mutation.error} action={existing ? 'save the property' : 'add the property'} />
      <TextField label="Property name" value={values.name} onChangeText={set('name')} error={errors.name} autoCapitalize="words" placeholder="e.g. Sunrise PG" />
      <TextField label="Address" value={values.address} onChangeText={set('address')} error={errors.address} multiline />
      <TextField label="City" value={values.city} onChangeText={set('city')} error={errors.city} autoCapitalize="words" />
      <TextField label="State (optional)" value={values.state} onChangeText={set('state')} error={errors.state} autoCapitalize="words" />
      <TextField label="PIN code (optional)" value={values.postalCode} onChangeText={set('postalCode')} error={errors.postalCode} keyboardType="number-pad" />
      <TextField
        label="Contact phone (optional)"
        value={values.contactPhone}
        onChangeText={set('contactPhone')}
        error={errors.contactPhone}
        keyboardType="phone-pad"
        hint="Shown on receipts."
      />
      <Button label={existing ? 'Save changes' : 'Add property'} onPress={submit} loading={mutation.isPending} />
    </FormScreen>
  );
}
