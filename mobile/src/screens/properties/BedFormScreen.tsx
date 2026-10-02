import { router } from 'expo-router';
import { useState } from 'react';
import { Alert } from 'react-native';

import { fieldErrorsFrom } from '@/api/errors';
import type { Bed } from '@/api/properties';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { FormScreen } from '@/components/FormScreen';
import { InlineError } from '@/components/InlineError';
import { LoadingState } from '@/components/LoadingState';
import { SegmentedControl } from '@/components/SegmentedControl';
import { TextField } from '@/components/TextField';
import { useAddBed, useArchiveBed, useBed, useUpdateBed } from '@/hooks/useProperties';
import { moneyToInput, parseMoney } from '@/utils/money';

const STATUS_OPTIONS = [
  { value: 'Available', label: 'Available' },
  { value: 'Reserved', label: 'Reserved' },
  { value: 'Unavailable', label: 'Unavailable' },
] as const;

type EditableStatus = (typeof STATUS_OPTIONS)[number]['value'];

export function AddBedScreen({ roomId }: { roomId: string }) {
  const [label, setLabel] = useState('');
  const [rent, setRent] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const add = useAddBed(roomId);

  const submit = () => {
    const parsed = parseMoney(rent);
    const found: Record<string, string> = {};
    if (label.trim().length > 20) found.label = 'Bed label can be at most 20 characters.';
    if (parsed.error) found.rent = parsed.error;
    setErrors(found);
    if (Object.keys(found).length > 0) return;

    add.mutate(
      { label: label.trim() || undefined, defaultMonthlyRent: parsed.value },
      { onSuccess: () => router.back(), onError: (error) => setErrors(fieldErrorsFrom(error)) },
    );
  };

  return (
    <FormScreen>
      <InlineError error={add.error} action="add the bed" />
      <TextField
        label="Bed label (optional)"
        value={label}
        onChangeText={setLabel}
        error={errors.label}
        autoCapitalize="characters"
        hint="Leave empty to use the next letter (A, B, C…)."
      />
      <TextField
        label="Monthly rent (optional)"
        value={rent}
        onChangeText={setRent}
        error={errors.rent ?? errors.defaultMonthlyRent}
        keyboardType="decimal-pad"
        placeholder="e.g. 8500"
      />
      <Button label="Add bed" onPress={submit} loading={add.isPending} />
    </FormScreen>
  );
}

export function EditBedScreen({ bedId }: { bedId: string }) {
  const query = useBed(bedId);
  if (query.isPending) return <LoadingState />;
  if (query.error) {
    return (
      <FormScreen>
        <ErrorState error={query.error} action="load this bed" onRetry={() => void query.refetch()} />
      </FormScreen>
    );
  }
  return <EditBedForm bed={query.data} />;
}

function EditBedForm({ bed }: { bed: Bed }) {
  const [label, setLabel] = useState(bed.label);
  const [status, setStatus] = useState<EditableStatus>(bed.status === 'Archived' ? 'Available' : bed.status);
  const [rent, setRent] = useState(moneyToInput(bed.defaultMonthlyRent));
  const [errors, setErrors] = useState<Record<string, string>>({});
  const update = useUpdateBed(bed.id);
  const archive = useArchiveBed(bed.id);

  const submit = () => {
    const parsed = parseMoney(rent);
    const found: Record<string, string> = {};
    if (!label.trim()) found.label = 'Enter the bed label.';
    else if (label.trim().length > 20) found.label = 'Bed label can be at most 20 characters.';
    if (parsed.error) found.rent = parsed.error;
    setErrors(found);
    if (Object.keys(found).length > 0) return;

    update.mutate(
      { label: label.trim(), status, defaultMonthlyRent: parsed.value },
      { onSuccess: () => router.back(), onError: (error) => setErrors(fieldErrorsFrom(error)) },
    );
  };

  const confirmArchive = () =>
    Alert.alert(`Archive bed ${bed.label}?`, 'It will be removed from the room. Nothing is deleted.', [
      { text: 'Cancel', style: 'cancel' },
      { text: 'Archive', style: 'destructive', onPress: () => archive.mutate(undefined, { onSuccess: () => router.back() }) },
    ]);

  return (
    <FormScreen>
      <InlineError error={update.error ?? archive.error} action="save the bed" />
      <TextField label="Bed label" value={label} onChangeText={setLabel} error={errors.label} autoCapitalize="characters" />
      <SegmentedControl label="Status" options={STATUS_OPTIONS} value={status} onChange={setStatus} />
      <TextField
        label="Monthly rent (optional)"
        value={rent}
        onChangeText={setRent}
        error={errors.rent ?? errors.defaultMonthlyRent}
        keyboardType="decimal-pad"
      />
      <Button label="Save changes" onPress={submit} loading={update.isPending} />
      {bed.occupancy === 'Vacant' || (bed.occupancy === 'Reserved' && !bed.tenant) ? (
        <Button
          label="Add a tenant to this bed"
          variant="secondary"
          onPress={() => router.push({ pathname: '/tenants/new', params: { bedId: bed.id } })}
        />
      ) : null}
      <Button label="Archive bed" variant="secondary" onPress={confirmArchive} loading={archive.isPending} />
    </FormScreen>
  );
}
