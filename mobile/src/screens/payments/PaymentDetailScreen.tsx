import { router } from 'expo-router';
import { useState } from 'react';
import { RefreshControl, StyleSheet, View } from 'react-native';

import { fieldErrorsFrom } from '@/api/errors';
import { methodLabel, type Payment, type PaymentSummary } from '@/api/payments';
import { can } from '@/auth/permissions';
import { useCurrentUser } from '@/auth/SessionProvider';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Card } from '@/components/Card';
import { ErrorState } from '@/components/ErrorState';
import { FormScreen } from '@/components/FormScreen';
import { InlineError } from '@/components/InlineError';
import { ListRow } from '@/components/ListRow';
import { StatusPill } from '@/components/StatusPill';
import { TextField } from '@/components/TextField';
import { GradientHero, SkeletonList } from '@/components/Visuals';
import { usePayment, useVoidPayment } from '@/hooks/usePayments';
import { periodLabel } from '@/hooks/useRent';
import { radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';
import { formatDate } from '@/utils/dates';
import { formatRupees } from '@/utils/money';

export function PaymentDetailScreen({ paymentId }: { paymentId: string }) {
  const query = usePayment(paymentId);
  if (query.isPending) {
    return (
      <FormScreen>
        <SkeletonList rows={3} />
      </FormScreen>
    );
  }
  if (query.error) {
    return (
      <FormScreen>
        <ErrorState error={query.error} action="load this payment" onRetry={() => void query.refetch()} />
      </FormScreen>
    );
  }
  return <PaymentDetail payment={query.data} refetch={query.refetch} refreshing={query.isRefetching} />;
}

function PaymentDetail({ payment: p, refetch, refreshing }: { payment: Payment; refetch: () => unknown; refreshing: boolean }) {
  const { colors } = useTheme();
  const user = useCurrentUser();
  const isOwner = user.role === 'Owner';
  const canSeeReceipt = can(user, 'RecordPayments') || can(user, 'GenerateReceipts');
  const voided = p.status === 'Voided';

  return (
    <FormScreen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refetch()} />}>
      <GradientHero>
        <AppText variant="overline" color="rgba(255,255,255,0.8)">
          {p.tenantName} paid
        </AppText>
        <AppText variant="display" color="#FFFFFF" style={voided ? styles.struck : undefined}>
          {formatRupees(p.amount)}
        </AppText>
        <View style={styles.heroRow}>
          <StatusPill label={voided ? 'Voided' : 'Recorded'} tone={voided ? 'danger' : 'success'} icon={voided ? 'ban' : 'checkmark-circle'} />
          <AppText variant="label" color="#FFFFFF">
            {formatDate(p.paymentDate)} · {methodLabel(p.method)}
          </AppText>
        </View>
      </GradientHero>

      {voided ? (
        <Card>
          <AppText variant="heading" color={colors.danger}>
            Voided
          </AppText>
          <AppText>{p.voidReason}</AppText>
          <AppText variant="caption" muted>
            The amounts below were taken off the dues again. The record is kept for your books.
          </AppText>
        </Card>
      ) : null}

      <Card>
        <AppText variant="heading">Paid towards</AppText>
        {p.allocations.map((a) => (
          <Line key={a.rentChargeId} label={periodLabel(a.periodStart)} value={formatRupees(a.amount)} />
        ))}
      </Card>

      <Card>
        <Line label="Receipt" value={p.receiptNumber} />
        {p.referenceNumber ? <Line label="Reference" value={p.referenceNumber} /> : null}
        {p.recordedByName ? <Line label="Recorded by" value={p.recordedByName} /> : null}
        {p.notes ? <Line label="Notes" value={p.notes} /> : null}
      </Card>

      <View style={[styles.list, { borderColor: colors.border }]}>
        <ListRow
          icon="person-outline"
          title={p.tenantName}
          subtitle="View tenant"
          onPress={() => router.push({ pathname: '/tenants/[id]', params: { id: p.tenantId } })}
        />
        {canSeeReceipt ? (
          <ListRow
            icon="document-text-outline"
            title={`Receipt ${p.receiptNumber}`}
            subtitle="View or share"
            onPress={() => router.push({ pathname: '/receipts/[id]', params: { id: p.receiptId } })}
          />
        ) : null}
      </View>

      {isOwner && !voided ? (
        <Button
          label="Void this payment"
          variant="danger"
          icon="ban-outline"
          onPress={() => router.push({ pathname: '/payments/[id]/void', params: { id: p.id } })}
        />
      ) : null}
    </FormScreen>
  );
}

/** Owner-only correction for a payment entered by mistake. Audited; the record and receipt are kept, marked VOID. */
export function VoidPaymentScreen({ paymentId }: { paymentId: string }) {
  const query = usePayment(paymentId);
  if (query.isPending) return <FormScreen><SkeletonList rows={2} /></FormScreen>;
  if (query.error) return <FormScreen><ErrorState error={query.error} action="load this payment" onRetry={() => void query.refetch()} /></FormScreen>;
  return <VoidForm payment={query.data} />;
}

function VoidForm({ payment }: { payment: Payment }) {
  const [reason, setReason] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const voidPayment = useVoidPayment(payment.id);

  const submit = () => {
    if (reason.trim().length < 3) {
      setErrors({ reason: 'Enter why, e.g. "Entered twice".' });
      return;
    }
    setErrors({});
    voidPayment.mutate(reason.trim(), { onSuccess: () => router.back(), onError: (e) => setErrors(fieldErrorsFrom(e)) });
  };

  return (
    <FormScreen>
      <AppText muted>
        Voiding {payment.tenantName}&apos;s payment of {formatRupees(payment.amount)} adds it back to what they owe and marks receipt{' '}
        {payment.receiptNumber} as VOID. The payment stays on record with your name and reason. This cannot be undone.
      </AppText>
      <InlineError error={voidPayment.error} action="void the payment" />
      <TextField label="Reason" value={reason} onChangeText={setReason} error={errors.reason} placeholder="e.g. Entered twice by mistake" maxLength={200} />
      <Button label="Void payment" variant="danger" icon="ban-outline" onPress={submit} loading={voidPayment.isPending} />
    </FormScreen>
  );
}

/** A compact payment line for tenant and charge screens. */
export function PaymentRow({
  payment,
  subtitlePrefix,
}: {
  payment: Pick<PaymentSummary, 'id' | 'amount' | 'paymentDate' | 'method' | 'status' | 'receiptNumber'>;
  /** E.g. the tenant's name, in lists that mix tenants. */
  subtitlePrefix?: string;
}) {
  const voided = payment.status === 'Voided';
  return (
    <ListRow
      icon={voided ? 'ban-outline' : 'cash-outline'}
      title={`${formatRupees(payment.amount)} · ${methodLabel(payment.method)}`}
      subtitle={`${subtitlePrefix ? `${subtitlePrefix} · ` : ''}${formatDate(payment.paymentDate)} · ${payment.receiptNumber}${voided ? ' · Voided' : ''}`}
      onPress={() => router.push({ pathname: '/payments/[id]', params: { id: payment.id } })}
    />
  );
}

function Line({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.line} accessible accessibilityLabel={`${label}: ${value}`}>
      <AppText muted style={styles.flex}>
        {label}
      </AppText>
      <AppText variant="label" style={styles.value}>
        {value}
      </AppText>
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  heroRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, flexWrap: 'wrap' },
  line: { flexDirection: 'row', alignItems: 'flex-start', gap: spacing.md },
  value: { flexShrink: 1, textAlign: 'right' },
  list: { borderRadius: radius.lg, overflow: 'hidden' },
  struck: { textDecorationLine: 'line-through' },
});
