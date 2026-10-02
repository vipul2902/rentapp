import { router } from 'expo-router';
import { useState } from 'react';

import { fieldErrorsFrom } from '@/api/errors';
import type { Room } from '@/api/properties';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { FormScreen } from '@/components/FormScreen';
import { InlineError } from '@/components/InlineError';
import { LoadingState } from '@/components/LoadingState';
import { SegmentedControl } from '@/components/SegmentedControl';
import { TextField } from '@/components/TextField';
import { ToggleRow } from '@/components/ToggleRow';
import { useCreateRoom, useRoom, useUpdateRoom } from '@/hooks/useProperties';
import { parseMoney } from '@/utils/money';
import { hasErrors, lengthBetween, required, validate, type Validator } from '@/utils/validation';

const capacityRule: Validator = (v) => {
  const n = Number(v);
  return /^\d{1,2}$/.test(v.trim()) && n >= 1 && n <= 50 ? undefined : 'Capacity must be between 1 and 50 beds.';
};

const ROOM_RULES = {
  roomNumber: [required('Enter the room number or name.'), lengthBetween(1, 50, 'Room number can be at most 50 characters.')],
  roomType: [(v: string) => (v.length <= 50 ? undefined : 'Room type can be at most 50 characters.')],
  capacity: [capacityRule],
};

const STATUS_OPTIONS = [
  { value: 'Active', label: 'In use' },
  { value: 'Unavailable', label: 'Unavailable' },
] as const;

export function CreateRoomScreen({ propertyId }: { propertyId: string }) {
  const [values, setValues] = useState({ roomNumber: '', roomType: '', capacity: '1', rent: '' });
  const [createBeds, setCreateBeds] = useState(true);
  const [errors, setErrors] = useState<Partial<Record<keyof typeof values | 'defaultMonthlyRent', string>>>({});
  const create = useCreateRoom(propertyId);

  const set = (field: keyof typeof values) => (text: string) => setValues((v) => ({ ...v, [field]: text }));

  const submit = () => {
    const found: typeof errors = validate(values, ROOM_RULES);
    const rent = parseMoney(values.rent);
    if (createBeds && rent.error) found.rent = rent.error;
    setErrors(found);
    if (hasErrors(found)) return;

    create.mutate(
      {
        roomNumber: values.roomNumber.trim(),
        roomType: values.roomType.trim() || undefined,
        capacity: Number(values.capacity),
        createBeds,
        defaultMonthlyRent: createBeds ? rent.value : undefined,
      },
      {
        onSuccess: (room) => router.replace({ pathname: '/rooms/[id]', params: { id: room.id } }),
        onError: (error) => setErrors(fieldErrorsFrom(error)),
      },
    );
  };

  return (
    <FormScreen>
      <InlineError error={create.error} action="add the room" />
      <TextField label="Room number or name" value={values.roomNumber} onChangeText={set('roomNumber')} error={errors.roomNumber} placeholder="e.g. 201" />
      <TextField label="Room type (optional)" value={values.roomType} onChangeText={set('roomType')} error={errors.roomType} placeholder="e.g. AC, Non-AC" />
      <TextField
        label="Number of beds"
        value={values.capacity}
        onChangeText={set('capacity')}
        error={errors.capacity}
        keyboardType="number-pad"
        hint="The most beds this room can hold."
      />
      <ToggleRow
        label="Create the beds now"
        description={`Adds beds labelled A, B, C… (${Number(values.capacity) > 0 ? values.capacity : 'N'} in total).`}
        value={createBeds}
        onChange={setCreateBeds}
      />
      {createBeds ? (
        <TextField
          label="Monthly rent per bed (optional)"
          value={values.rent}
          onChangeText={set('rent')}
          error={errors.rent ?? errors.defaultMonthlyRent}
          keyboardType="decimal-pad"
          placeholder="e.g. 8500"
          hint="Suggested rent, pre-filled when you add a tenant."
        />
      ) : null}
      <Button label="Add room" onPress={submit} loading={create.isPending} />
    </FormScreen>
  );
}

export function EditRoomScreen({ roomId }: { roomId: string }) {
  const query = useRoom(roomId);
  if (query.isPending) return <LoadingState />;
  if (query.error) {
    return (
      <FormScreen>
        <ErrorState error={query.error} action="load this room" onRetry={() => void query.refetch()} />
      </FormScreen>
    );
  }
  return <EditRoomForm room={query.data} />;
}

function EditRoomForm({ room }: { room: Room }) {
  const [values, setValues] = useState({ roomNumber: room.roomNumber, roomType: room.roomType ?? '', capacity: String(room.capacity) });
  const [status, setStatus] = useState<'Active' | 'Unavailable'>(room.status === 'Unavailable' ? 'Unavailable' : 'Active');
  const [errors, setErrors] = useState<Partial<Record<keyof typeof values, string>>>({});
  const update = useUpdateRoom(room.id);

  const set = (field: keyof typeof values) => (text: string) => setValues((v) => ({ ...v, [field]: text }));

  const submit = () => {
    const found = validate(values, ROOM_RULES);
    setErrors(found);
    if (hasErrors(found)) return;
    update.mutate(
      { roomNumber: values.roomNumber.trim(), roomType: values.roomType.trim() || undefined, capacity: Number(values.capacity), status },
      { onSuccess: () => router.back(), onError: (error) => setErrors(fieldErrorsFrom(error)) },
    );
  };

  return (
    <FormScreen>
      <InlineError error={update.error} action="save the room" />
      <TextField label="Room number or name" value={values.roomNumber} onChangeText={set('roomNumber')} error={errors.roomNumber} />
      <TextField label="Room type (optional)" value={values.roomType} onChangeText={set('roomType')} error={errors.roomType} />
      <TextField
        label="Capacity (beds)"
        value={values.capacity}
        onChangeText={set('capacity')}
        error={errors.capacity}
        keyboardType="number-pad"
        hint={`Currently ${room.beds.length} ${room.beds.length === 1 ? 'bed' : 'beds'}. Capacity cannot be lower.`}
      />
      <SegmentedControl label="Status" options={STATUS_OPTIONS} value={status} onChange={setStatus} />
      <Button label="Save changes" onPress={submit} loading={update.isPending} />
    </FormScreen>
  );
}
