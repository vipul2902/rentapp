import { router } from 'expo-router';
import { useState } from 'react';

import { fieldErrorsFrom } from '@/api/errors';
import type { TenantDetail } from '@/api/tenants';
import { AppText } from '@/components/AppText';
import { BedPicker } from '@/components/BedPicker';
import { Button } from '@/components/Button';
import { Card } from '@/components/Card';
import { DateField } from '@/components/DateField';
import { ErrorState } from '@/components/ErrorState';
import { FormScreen } from '@/components/FormScreen';
import { InlineError } from '@/components/InlineError';
import { LoadingState } from '@/components/LoadingState';
import { TextField } from '@/components/TextField';
import { useMoveIn, useMoveOut, useMoveTenant, useTenant, useUpdateTerms } from '@/hooks/useTenants';
import { formatDate, todayIso } from '@/utils/dates';
import { formatRupees, MONEY_MESSAGE, moneyToInput, parseMoney } from '@/utils/money';

import { AssignBedFields, useAssignBedForm } from './AssignBedFields';

/** Loads the tenant, then renders the given form. */
function WithTenant({ tenantId, children }: { tenantId: string; children: (tenant: TenantDetail) => React.ReactNode }) {
  const query = useTenant(tenantId);
  if (query.isPending) return <LoadingState />;
  if (query.error) {
    return (
      <FormScreen>
        <ErrorState error={query.error} action="load this tenant" onRetry={() => void query.refetch()} />
      </FormScreen>
    );
  }
  return <>{children(query.data)}</>;
}

// ---- Assign a bed -------------------------------------------------------------------------------

export function MoveInScreen({ tenantId }: { tenantId: string }) {
  const form = useAssignBedForm();
  const moveIn = useMoveIn(tenantId);

  const submit = () => {
    const input = form.validate();
    if (!input) return;
    moveIn.mutate(input, { onSuccess: () => router.back(), onError: (error) => form.setErrors(fieldErrorsFrom(error)) });
  };

  return (
    <FormScreen>
      <InlineError error={moveIn.error} action="assign the bed" />
      <AssignBedFields form={form} />
      <Button label="Assign bed" onPress={submit} loading={moveIn.isPending} />
    </FormScreen>
  );
}

// ---- Move out / cancel booking ------------------------------------------------------------------

export function MoveOutScreen({ tenantId }: { tenantId: string }) {
  return <WithTenant tenantId={tenantId}>{(tenant) => <MoveOutForm tenant={tenant} />}</WithTenant>;
}

function MoveOutForm({ tenant }: { tenant: TenantDetail }) {
  const tenancy = tenant.currentTenancy;
  const today = todayIso();
  const [date, setDate] = useState(today);
  const [error, setError] = useState<string>();
  const moveOut = useMoveOut(tenant.id);

  if (!tenancy) {
    return (
      <FormScreen>
        <AppText>{tenant.fullName} does not have a bed.</AppText>
      </FormScreen>
    );
  }

  const upcoming = tenancy.state === 'Upcoming';
  const submit = () =>
    moveOut.mutate(upcoming ? tenancy.startDate : date, {
      onSuccess: () => router.back(),
      onError: (e) => setError(fieldErrorsFrom(e).moveOutDate),
    });

  return (
    <FormScreen>
      <Card>
        <AppText variant="heading">
          {tenant.fullName} · Room {tenancy.roomNumber} · Bed {tenancy.bedLabel}
        </AppText>
        <AppText muted>
          {upcoming
            ? `Booked to move in on ${formatDate(tenancy.startDate)}. Cancelling frees the bed; nothing will be charged.`
            : `Moved in on ${formatDate(tenancy.startDate)}. The bed becomes vacant once you confirm.`}
        </AppText>
      </Card>
      {upcoming ? null : (
        <DateField
          label="Last day in the bed"
          value={date}
          onChange={setDate}
          minimumDate={tenancy.startDate}
          maximumDate={today}
          error={error}
        />
      )}
      <InlineError error={moveOut.error} action={upcoming ? 'cancel the booking' : 'move the tenant out'} />
      <Button label={upcoming ? 'Cancel booking' : 'Confirm move-out'} onPress={submit} loading={moveOut.isPending} />
    </FormScreen>
  );
}

// ---- Move to another bed ------------------------------------------------------------------------

export function MoveTenantScreen({ tenantId }: { tenantId: string }) {
  return <WithTenant tenantId={tenantId}>{(tenant) => <MoveTenantForm tenant={tenant} />}</WithTenant>;
}

function MoveTenantForm({ tenant }: { tenant: TenantDetail }) {
  const tenancy = tenant.currentTenancy;
  const today = todayIso();
  const [bedId, setBedId] = useState<string>();
  const [date, setDate] = useState(today);
  const [rent, setRent] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const move = useMoveTenant(tenant.id);

  if (!tenancy) {
    return (
      <FormScreen>
        <AppText>{tenant.fullName} does not have a bed.</AppText>
      </FormScreen>
    );
  }

  const upcoming = tenancy.state === 'Upcoming';
  const submit = () => {
    const parsed = parseMoney(rent);
    const found: Record<string, string> = {};
    if (!bedId) found.bedId = 'Choose the new bed.';
    if (parsed.error) found.monthlyRent = parsed.error;
    setErrors(found);
    if (!bedId || Object.keys(found).length > 0) return;
    move.mutate(
      { bedId, moveDate: upcoming ? tenancy.startDate : date, monthlyRent: parsed.value },
      { onSuccess: () => router.back(), onError: (e) => setErrors(fieldErrorsFrom(e)) },
    );
  };

  return (
    <FormScreen>
      <AppText muted>
        Now in {tenancy.propertyName} · Room {tenancy.roomNumber} · Bed {tenancy.bedLabel} at {formatRupees(tenancy.monthlyRent)}/month.
        The deposit and due day stay the same.
      </AppText>
      <BedPicker
        value={bedId}
        excludeBedId={tenancy.bedId}
        onChange={(bed) => {
          setBedId(bed.bedId);
          if (!rent && bed.defaultMonthlyRent !== null) setRent(moneyToInput(bed.defaultMonthlyRent));
        }}
        error={errors.bedId}
      />
      {upcoming ? null : (
        <DateField label="First day in the new bed" value={date} onChange={setDate} minimumDate={tenancy.startDate} maximumDate={today} error={errors.moveDate} />
      )}
      <TextField
        label="New monthly rent (optional)"
        value={rent}
        onChangeText={setRent}
        error={errors.monthlyRent}
        keyboardType="decimal-pad"
        hint="Leave empty to use the new bed's suggested rent, or the current rent."
      />
      <InlineError error={move.error} action="move the tenant" />
      <Button label="Move tenant" onPress={submit} loading={move.isPending} />
    </FormScreen>
  );
}

// ---- Rent, deposit and due day ------------------------------------------------------------------

export function TenancyTermsScreen({ tenantId }: { tenantId: string }) {
  return <WithTenant tenantId={tenantId}>{(tenant) => <TermsForm tenant={tenant} />}</WithTenant>;
}

function TermsForm({ tenant }: { tenant: TenantDetail }) {
  const tenancy = tenant.currentTenancy;
  const [rent, setRent] = useState(moneyToInput(tenancy?.monthlyRent));
  const [deposit, setDeposit] = useState(moneyToInput(tenancy?.securityDeposit));
  const [dueDay, setDueDay] = useState(String(tenancy?.rentDueDay ?? 5));
  const [errors, setErrors] = useState<Record<string, string>>({});
  const update = useUpdateTerms(tenant.id);

  if (!tenancy) {
    return (
      <FormScreen>
        <AppText>{tenant.fullName} does not have a bed.</AppText>
      </FormScreen>
    );
  }

  const submit = () => {
    const r = parseMoney(rent);
    const d = deposit.trim() === '' || deposit.trim() === '0' ? { value: 0 } : parseMoney(deposit);
    const day = Number(dueDay);
    const found: Record<string, string> = {};
    if (r.error || r.value === undefined) found.monthlyRent = r.error ?? 'Enter the monthly rent.';
    if (d.error) found.securityDeposit = MONEY_MESSAGE;
    if (!/^\d{1,2}$/.test(dueDay) || day < 1 || day > 31) found.rentDueDay = 'Enter a day between 1 and 31.';
    setErrors(found);
    if (Object.keys(found).length > 0 || r.value === undefined) return;
    update.mutate(
      { monthlyRent: r.value, securityDeposit: d.value ?? 0, rentDueDay: day },
      { onSuccess: () => router.back(), onError: (e) => setErrors(fieldErrorsFrom(e)) },
    );
  };

  return (
    <FormScreen>
      <InlineError error={update.error} action="save the changes" />
      <TextField label="Monthly rent" value={rent} onChangeText={setRent} error={errors.monthlyRent} keyboardType="decimal-pad" />
      <TextField label="Security deposit" value={deposit} onChangeText={setDeposit} error={errors.securityDeposit} keyboardType="decimal-pad" />
      <TextField label="Rent due on day" value={dueDay} onChangeText={setDueDay} error={errors.rentDueDay} keyboardType="number-pad" />
      <AppText variant="caption" muted>
        Changes apply to rent dues created from now on. Past dues are not changed.
      </AppText>
      <Button label="Save changes" onPress={submit} loading={update.isPending} />
    </FormScreen>
  );
}
