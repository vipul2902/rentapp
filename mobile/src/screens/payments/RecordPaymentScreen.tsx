import { router } from 'expo-router';
import { useState } from 'react';
import { StyleSheet, TextInput, View } from 'react-native';

import { fieldErrorsFrom } from '@/api/errors';
import { PAYMENT_METHODS, type PaymentMethod } from '@/api/payments';
import type { TenantDetail, TenantFilter } from '@/api/tenants';
import { AppText } from '@/components/AppText';
import { Avatar } from '@/components/Avatar';
import { Button } from '@/components/Button';
import { Card } from '@/components/Card';
import { DateField } from '@/components/DateField';
import { ErrorState } from '@/components/ErrorState';
import { FormScreen } from '@/components/FormScreen';
import type { IconName } from '@/components/Icon';
import { InlineError } from '@/components/InlineError';
import { ListRow } from '@/components/ListRow';
import { PressableScale } from '@/components/PressableScale';
import { SearchField } from '@/components/SearchField';
import { TextField } from '@/components/TextField';
import { ChipBar, EmptyState, SkeletonList } from '@/components/Visuals';
import { useRecordPayment } from '@/hooks/usePayments';
import { periodLabel, useCharge } from '@/hooks/useRent';
import { useTenant, useTenantList } from '@/hooks/useTenants';
import { radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';
import { todayIso } from '@/utils/dates';
import { formatRupees, moneyToInput, parseMoney } from '@/utils/money';

const METHOD_ICONS: Record<PaymentMethod, IconName> = {
  Upi: 'phone-portrait-outline',
  Cash: 'cash-outline',
  BankTransfer: 'business-outline',
  Card: 'card-outline',
  Other: 'ellipsis-horizontal-circle-outline',
};

/**
 * The most-used screen in the app: built to record a payment in a few taps. The amount is prefilled
 * with what is owed (or the chosen due), UPI is preselected, and the date defaults to today.
 */
export function RecordPaymentScreen({ tenantId, chargeId }: { tenantId?: string; chargeId?: string }) {
  return tenantId ? <RecordForTenant tenantId={tenantId} chargeId={chargeId} /> : <TenantPicker />;
}

const PICKER_FILTERS = [
  { value: 'Overdue', label: 'Owe rent', icon: 'alert-circle-outline' },
  { value: 'Current', label: 'All current', icon: 'home-outline' },
] as const;

/** Started from Home: first choose who paid. Overdue tenants are listed first, since they are the usual case. */
function TenantPicker() {
  const { colors } = useTheme();
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState<TenantFilter>('Overdue');
  const query = useTenantList(search, filter);
  const tenants = query.data?.pages.flatMap((p) => p.items) ?? [];

  return (
    <FormScreen>
      <AppText variant="heading">Who paid?</AppText>
      <SearchField placeholder="Name, phone or room number" onSearch={setSearch} />
      <ChipBar label="Show" options={PICKER_FILTERS} value={filter} onChange={setFilter} />
      {query.isPending ? (
        <SkeletonList rows={3} />
      ) : query.error ? (
        <ErrorState error={query.error} action="load tenants" onRetry={() => void query.refetch()} />
      ) : tenants.length === 0 ? (
        <EmptyState icon="happy-outline" title={filter === 'Overdue' ? 'Nobody owes overdue rent' : 'No tenants found'} />
      ) : (
        <View style={[styles.list, { borderColor: colors.border }]}>
          {tenants.map((t) => (
            <ListRow
              key={t.id}
              leading={<Avatar name={t.fullName} size={40} />}
              title={t.fullName}
              subtitle={`${t.outstandingAmount > 0 ? `Owes ${formatRupees(t.outstandingAmount)}` : 'Nothing owed'}${t.currentTenancy ? ` · Room ${t.currentTenancy.roomNumber}` : ''}`}
              onPress={() => router.setParams({ tenantId: t.id })}
            />
          ))}
        </View>
      )}
    </FormScreen>
  );
}

function RecordForTenant({ tenantId, chargeId }: { tenantId: string; chargeId?: string }) {
  const tenant = useTenant(tenantId);
  const charge = useCharge(chargeId ?? '', Boolean(chargeId));
  if (tenant.isPending || (chargeId && charge.isPending)) {
    return (
      <FormScreen>
        <SkeletonList rows={3} />
      </FormScreen>
    );
  }
  const error = tenant.error ?? (chargeId ? charge.error : null);
  if (error || !tenant.data) {
    return (
      <FormScreen>
        <ErrorState error={error} action="load this tenant" onRetry={() => void tenant.refetch()} />
      </FormScreen>
    );
  }
  const due = chargeId && charge.data ? charge.data.charge : undefined;
  return <RecordPaymentForm tenant={tenant.data} due={due ? { id: due.id, balance: due.balance, period: periodLabel(due.periodStart) } : undefined} />;
}

function RecordPaymentForm({ tenant, due }: { tenant: TenantDetail; due?: { id: string; balance: number; period: string } }) {
  const { colors } = useTheme();
  const owed = due ? due.balance : tenant.outstandingAmount;
  const [amount, setAmount] = useState(moneyToInput(owed));
  const [method, setMethod] = useState<PaymentMethod>('Upi');
  const [paymentDate, setPaymentDate] = useState(todayIso());
  const [reference, setReference] = useState('');
  const [notes, setNotes] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const record = useRecordPayment();

  if (tenant.outstandingAmount <= 0) {
    return (
      <FormScreen>
        <EmptyState icon="checkmark-done-circle-outline" title={`${tenant.fullName} is all paid up`} message="There are no dues to record a payment against." />
      </FormScreen>
    );
  }

  const quick = [
    due ? { label: `${due.period} ${formatRupees(due.balance)}`, value: due.balance } : null,
    { label: `All dues ${formatRupees(tenant.outstandingAmount)}`, value: tenant.outstandingAmount },
    tenant.overdueAmount > 0 && tenant.overdueAmount !== tenant.outstandingAmount
      ? { label: `Overdue ${formatRupees(tenant.overdueAmount)}`, value: tenant.overdueAmount }
      : null,
  ].filter((q): q is { label: string; value: number } => q !== null);

  const parsed = parseMoney(amount);
  const submit = () => {
    const found: Record<string, string> = {};
    if (parsed.error || parsed.value === undefined) found.amount = parsed.error ?? 'Enter the amount received.';
    setErrors(found);
    if (Object.keys(found).length > 0 || parsed.value === undefined) return;
    record.mutate(
      {
        tenantId: tenant.id,
        amount: parsed.value,
        paymentDate,
        method,
        referenceNumber: reference.trim() || undefined,
        notes: notes.trim() || undefined,
        chargeIds: due ? [due.id] : undefined,
      },
      {
        onSuccess: (result) => router.replace({ pathname: '/receipts/[id]', params: { id: result.receipt.id, recorded: '1' } }),
        onError: (e) => setErrors(fieldErrorsFrom(e)),
      },
    );
  };

  return (
    <FormScreen>
      <View style={styles.who}>
        <Avatar name={tenant.fullName} size={44} />
        <View style={styles.flex}>
          <AppText variant="heading">{tenant.fullName}</AppText>
          <AppText muted>
            {due ? `Paying ${due.period}` : `Owes ${formatRupees(tenant.outstandingAmount)}`}
            {tenant.currentTenancy ? ` · Room ${tenant.currentTenancy.roomNumber}` : ''}
          </AppText>
        </View>
      </View>

      <Card>
        <AppText variant="overline" muted>
          Amount received
        </AppText>
        <View style={[styles.amountRow, { borderColor: errors.amount ? colors.danger : colors.border }]}>
          <AppText variant="display" color={colors.textMuted}>
            ₹
          </AppText>
          <TextInput
            accessibilityLabel="Amount received"
            accessibilityHint={errors.amount}
            value={amount}
            onChangeText={setAmount}
            keyboardType="decimal-pad"
            selectTextOnFocus
            style={[styles.amountInput, { color: colors.text }]}
          />
        </View>
        {errors.amount ? (
          <AppText variant="caption" color={colors.danger}>
            {errors.amount}
          </AppText>
        ) : null}
        <View style={styles.quick}>
          {quick.map((q) => (
            <PressableScale
              key={q.label}
              accessibilityRole="button"
              accessibilityLabel={`Use ${q.label}`}
              onPress={() => setAmount(moneyToInput(q.value))}
              style={[styles.quickChip, { backgroundColor: colors.primarySoft }]}
            >
              <AppText variant="caption" color={colors.primary}>
                {q.label}
              </AppText>
            </PressableScale>
          ))}
        </View>
      </Card>

      <View style={styles.field}>
        <AppText variant="label">Paid by</AppText>
        <ChipBar
          label="Payment method"
          options={PAYMENT_METHODS.map((m) => ({ value: m.value, label: m.label, icon: METHOD_ICONS[m.value] }))}
          value={method}
          onChange={setMethod}
        />
      </View>

      <DateField label="Payment date" value={paymentDate} onChange={setPaymentDate} maximumDate={todayIso()} error={errors.paymentDate} />
      {method !== 'Cash' ? (
        <TextField
          label={method === 'Upi' ? 'UPI transaction ID (optional)' : 'Reference number (optional)'}
          value={reference}
          onChangeText={setReference}
          error={errors.referenceNumber}
          autoCapitalize="characters"
          maxLength={100}
        />
      ) : null}
      <TextField label="Notes (optional)" value={notes} onChangeText={setNotes} error={errors.notes} maxLength={500} />

      <InlineError error={record.error} action="record the payment" />
      <Button
        label={parsed.value !== undefined ? `Record ${formatRupees(parsed.value)}` : 'Record payment'}
        icon="checkmark-circle-outline"
        onPress={submit}
        loading={record.isPending}
      />
      <AppText variant="caption" muted style={styles.center}>
        The oldest dues are settled first. A receipt is created automatically.
      </AppText>
    </FormScreen>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  center: { textAlign: 'center' },
  who: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  amountRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.xs, borderBottomWidth: 2, paddingBottom: spacing.xs },
  amountInput: { flex: 1, fontSize: 40, fontWeight: '800', paddingVertical: spacing.xs },
  quick: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  quickChip: { borderRadius: radius.pill, paddingHorizontal: spacing.md, paddingVertical: spacing.xs + 2 },
  field: { gap: spacing.sm },
  list: { borderRadius: radius.lg, overflow: 'hidden' },
});
