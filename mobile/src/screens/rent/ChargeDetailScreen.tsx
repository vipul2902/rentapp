import { router } from 'expo-router';
import { useState } from 'react';
import { Linking, RefreshControl, StyleSheet, View } from 'react-native';

import { fieldErrorsFrom } from '@/api/errors';
import type { RentChargeDetail } from '@/api/rent';
import { useCurrentUser } from '@/auth/SessionProvider';
import { AppText } from '@/components/AppText';
import { Avatar } from '@/components/Avatar';
import { Button } from '@/components/Button';
import { Card } from '@/components/Card';
import { ErrorState } from '@/components/ErrorState';
import { FormScreen } from '@/components/FormScreen';
import { InlineError } from '@/components/InlineError';
import { ListRow } from '@/components/ListRow';
import { StatusPill } from '@/components/StatusPill';
import { TextField } from '@/components/TextField';
import { GradientHero, SkeletonList } from '@/components/Visuals';
import { periodLabel, RENT_STATUS, useCharge, useWaive } from '@/hooks/useRent';
import { radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';
import { formatDate } from '@/utils/dates';
import { formatRupees, moneyToInput, parseMoney } from '@/utils/money';

export function ChargeDetailScreen({ chargeId }: { chargeId: string }) {
  const query = useCharge(chargeId);
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
        <ErrorState error={query.error} action="load this rent due" onRetry={() => void query.refetch()} />
      </FormScreen>
    );
  }
  return <ChargeDetail detail={query.data} refetch={query.refetch} refreshing={query.isRefetching} />;
}

function ChargeDetail({ detail, refetch, refreshing }: { detail: RentChargeDetail; refetch: () => unknown; refreshing: boolean }) {
  const { colors } = useTheme();
  const isOwner = useCurrentUser().role === 'Owner';
  const c = detail.charge;
  const status = RENT_STATUS[c.status];
  const open = c.balance > 0 && c.status !== 'Cancelled';

  return (
    <FormScreen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refetch()} />}>
      <GradientHero>
        <View style={styles.heroTop}>
          <Avatar name={c.tenantName} size={48} />
          <View style={styles.flex}>
            <AppText variant="heading" color="#FFFFFF">
              {c.tenantName}
            </AppText>
            <AppText color="rgba(255,255,255,0.85)">
              {periodLabel(c.periodStart)} · Room {c.roomNumber} · Bed {c.bedLabel}
            </AppText>
          </View>
        </View>
        <AppText variant="overline" color="rgba(255,255,255,0.8)">
          {open ? 'Left to pay' : 'Rent for the month'}
        </AppText>
        <AppText variant="display" color="#FFFFFF" accessibilityLabel={`Balance ${formatRupees(c.balance)}`}>
          {formatRupees(open ? c.balance : c.amount)}
        </AppText>
        <View style={styles.heroPill}>
          <StatusPill label={status.label} tone={status.tone} icon={status.icon} />
          {c.daysOverdue > 0 ? (
            <AppText variant="label" color="#FFFFFF">
              {c.daysOverdue} days late
            </AppText>
          ) : null}
        </View>
      </GradientHero>

      <Card>
        <Line label="Monthly rent" value={formatRupees(c.amount)} />
        {c.adjustedAmount > 0 ? <Line label="Waived" value={`− ${formatRupees(c.adjustedAmount)}`} /> : null}
        <Line label="Paid" value={c.paidAmount > 0 ? `− ${formatRupees(c.paidAmount)}` : formatRupees(0)} />
        <View style={[styles.divider, { backgroundColor: colors.border }]} />
        <Line label="Balance" value={formatRupees(c.balance)} strong />
        <Line label="Due on" value={formatDate(c.dueDate)} />
        <Line label="Property" value={c.propertyName} />
      </Card>

      <View style={[styles.list, { borderColor: colors.border }]}>
        <ListRow
          icon="person-outline"
          title={c.tenantName}
          subtitle="View tenant"
          onPress={() => router.push({ pathname: '/tenants/[id]', params: { id: c.tenantId } })}
        />
        <ListRow
          icon="call-outline"
          title={`Call ${c.tenantName.split(' ')[0]}`}
          subtitle={c.tenantPhone}
          onPress={() => void Linking.openURL(`tel:${c.tenantPhone.replace(/[^\d+]/g, '')}`)}
        />
      </View>

      {detail.adjustments.length > 0 ? (
        <Card>
          <AppText variant="heading">Waivers</AppText>
          {detail.adjustments.map((a) => (
            <Line key={a.id} label={a.reason} value={`− ${formatRupees(a.amount)}`} />
          ))}
        </Card>
      ) : null}

      {open ? (
        <View style={styles.actions}>
          <Button label="Record payment" icon="cash-outline" onPress={() => undefined} disabled />
          <AppText variant="caption" muted style={styles.center}>
            Recording payments and receipts arrives in the next update.
          </AppText>
          {isOwner ? (
            <Button
              label="Waive an amount"
              icon="gift-outline"
              variant="secondary"
              onPress={() => router.push({ pathname: '/rent/[id]/waive', params: { id: c.id } })}
            />
          ) : null}
        </View>
      ) : null}
    </FormScreen>
  );
}

function Line({ label, value, strong = false }: { label: string; value: string; strong?: boolean }) {
  return (
    <View style={styles.line} accessible accessibilityLabel={`${label}: ${value}`}>
      <AppText muted={!strong} variant={strong ? 'heading' : 'body'} style={styles.flex}>
        {label}
      </AppText>
      <AppText variant={strong ? 'heading' : 'label'}>{value}</AppText>
    </View>
  );
}

/** Owner-only: reduce what the tenant owes, with a reason (e.g. moved in mid-month). Audited. */
export function WaiveScreen({ chargeId }: { chargeId: string }) {
  const query = useCharge(chargeId);
  if (query.isPending) return <FormScreen><SkeletonList rows={2} /></FormScreen>;
  if (query.error) return <FormScreen><ErrorState error={query.error} action="load this rent due" onRetry={() => void query.refetch()} /></FormScreen>;
  return <WaiveForm detail={query.data} />;
}

function WaiveForm({ detail }: { detail: RentChargeDetail }) {
  const c = detail.charge;
  const [amount, setAmount] = useState(moneyToInput(c.balance));
  const [reason, setReason] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const waive = useWaive(c.id);

  const submit = () => {
    const parsed = parseMoney(amount);
    const found: Record<string, string> = {};
    if (parsed.error || parsed.value === undefined) found.amount = parsed.error ?? 'Enter the amount to waive.';
    if (reason.trim().length < 3) found.reason = 'Enter a reason, e.g. "Moved in on the 20th".';
    setErrors(found);
    if (Object.keys(found).length > 0 || parsed.value === undefined) return;
    waive.mutate({ amount: parsed.value, reason: reason.trim() }, { onSuccess: () => router.back(), onError: (e) => setErrors(fieldErrorsFrom(e)) });
  };

  return (
    <FormScreen>
      <AppText muted>
        {c.tenantName} owes {formatRupees(c.balance)} for {periodLabel(c.periodStart)}. Waiving reduces this; it is recorded with your name.
      </AppText>
      <InlineError error={waive.error} action="waive the amount" />
      <TextField label="Amount to waive" value={amount} onChangeText={setAmount} error={errors.amount} keyboardType="decimal-pad" />
      <TextField label="Reason" value={reason} onChangeText={setReason} error={errors.reason} placeholder="e.g. Moved in on the 20th" />
      <Button label="Waive amount" icon="gift-outline" onPress={submit} loading={waive.isPending} />
    </FormScreen>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  center: { textAlign: 'center' },
  heroTop: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, marginBottom: spacing.sm },
  heroPill: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  line: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  divider: { height: StyleSheet.hairlineWidth },
  list: { borderRadius: radius.lg, overflow: 'hidden' },
  actions: { gap: spacing.sm },
});
