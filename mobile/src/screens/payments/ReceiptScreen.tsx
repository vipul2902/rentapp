import * as Haptics from 'expo-haptics';
import { router } from 'expo-router';
import { useEffect, useState } from 'react';
import { Animated, Easing, StyleSheet, View } from 'react-native';

import { methodLabel, type Receipt } from '@/api/payments';
import { can } from '@/auth/permissions';
import { useCurrentUser } from '@/auth/SessionProvider';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { FadeIn } from '@/components/FadeIn';
import { FormScreen } from '@/components/FormScreen';
import { Icon } from '@/components/Icon';
import { InlineError } from '@/components/InlineError';
import { SkeletonList } from '@/components/Visuals';
import { useReceipt, useShareReceipt } from '@/hooks/usePayments';
import { elevation, radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';
import { formatDate } from '@/utils/dates';
import { formatRupees } from '@/utils/money';

/** A receipt, laid out like the PDF. Straight after recording a payment it also celebrates the success. */
export function ReceiptScreen({ receiptId, justRecorded = false }: { receiptId: string; justRecorded?: boolean }) {
  const query = useReceipt(receiptId);
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
        <ErrorState error={query.error} action="load this receipt" onRetry={() => void query.refetch()} />
      </FormScreen>
    );
  }
  return <ReceiptView receipt={query.data} justRecorded={justRecorded} />;
}

function ReceiptView({ receipt, justRecorded }: { receipt: Receipt; justRecorded: boolean }) {
  const share = useShareReceipt();
  const canViewPayment = can(useCurrentUser(), 'ViewTenants');
  const shareReceipt = () => share.mutate({ receiptId: receipt.id, receiptNumber: receipt.receiptNumber });

  return (
    <FormScreen>
      {justRecorded ? <Success receipt={receipt} /> : null}

      <FadeIn delay={justRecorded ? 200 : 0}>
        <ReceiptCard receipt={receipt} />
      </FadeIn>

      <InlineError error={share.error} action="share the receipt" />
      <View style={styles.actions}>
        <Button label="Share receipt (PDF)" icon="share-social-outline" onPress={shareReceipt} loading={share.isPending} />
        {justRecorded ? (
          <Button label="Done" variant="secondary" icon="checkmark" onPress={() => router.back()} />
        ) : canViewPayment ? (
          <Button
            label="View payment"
            variant="secondary"
            icon="receipt-outline"
            onPress={() => router.push({ pathname: '/payments/[id]', params: { id: receipt.paymentId } })}
          />
        ) : null}
      </View>
    </FormScreen>
  );
}

function Success({ receipt }: { receipt: Receipt }) {
  const { colors } = useTheme();
  const [pop] = useState(() => new Animated.Value(0));

  useEffect(() => {
    void Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success).catch(() => undefined);
    const animation = Animated.timing(pop, { toValue: 1, duration: 420, easing: Easing.out(Easing.back(2)), useNativeDriver: true });
    animation.start();
    return () => animation.stop();
  }, [pop]);

  return (
    <View style={styles.success} accessible accessibilityRole="alert" accessibilityLabel={`Payment recorded successfully. ${formatRupees(receipt.amount)}. Receipt ${receipt.receiptNumber}`}>
      <Animated.View style={[styles.check, { backgroundColor: colors.success, transform: [{ scale: pop }] }]}>
        <Icon name="checkmark" size={44} color="#FFFFFF" />
      </Animated.View>
      <AppText variant="title" style={styles.center}>
        Payment recorded successfully
      </AppText>
      <AppText variant="display" style={styles.center}>
        {formatRupees(receipt.amount)}
      </AppText>
      <AppText muted style={styles.center}>
        Receipt #{receipt.receiptNumber}
      </AppText>
    </View>
  );
}

function ReceiptCard({ receipt: r }: { receipt: Receipt }) {
  const { colors } = useTheme();
  return (
    <View style={[styles.card, { backgroundColor: colors.surface, borderColor: colors.border }, elevation(colors.shadow)]}>
      <View style={[styles.cardHeader, { backgroundColor: colors.primary }]}>
        <View style={styles.flex}>
          <AppText variant="heading" color="#FFFFFF">
            {r.organizationName}
          </AppText>
          <AppText variant="caption" color="rgba(255,255,255,0.85)">
            {r.propertyName}
          </AppText>
        </View>
        <View style={styles.headerRight}>
          <AppText variant="overline" color="rgba(255,255,255,0.85)">
            Receipt
          </AppText>
          <AppText variant="label" color="#FFFFFF">
            {r.receiptNumber}
          </AppText>
        </View>
      </View>

      {r.isVoid ? (
        <View style={[styles.void, { backgroundColor: colors.dangerSurface }]} accessibilityRole="alert">
          <Icon name="ban-outline" size={18} color={colors.danger} />
          <AppText variant="label" color={colors.danger} style={styles.flex}>
            VOID: this payment was reversed. The receipt is no longer valid.
          </AppText>
        </View>
      ) : null}

      <View style={styles.cardBody}>
        <Row label="Received from" value={r.tenantName} />
        <Row label="Phone" value={r.tenantPhone} />
        <Row label="Room / bed" value={`Room ${r.roomNumber} · Bed ${r.bedLabel}`} />
        <Row label="Rent period" value={r.periodLabel} />
        <Row label="Payment date" value={formatDate(r.paymentDate)} />
        <Row label="Method" value={methodLabel(r.method)} />
        {r.referenceNumber ? <Row label="Reference" value={r.referenceNumber} /> : null}
        <View style={[styles.divider, { backgroundColor: colors.border }]} />
        <View style={styles.amount}>
          <AppText variant="label" muted>
            Amount received
          </AppText>
          <AppText variant="title" style={r.isVoid ? styles.struck : undefined}>
            {formatRupees(r.amount)}
          </AppText>
        </View>
        <AppText variant="caption" muted>
          Issued {formatDate(r.issuedOn)} · {r.propertyAddress}
        </AppText>
      </View>
    </View>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.row} accessible accessibilityLabel={`${label}: ${value}`}>
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
  center: { textAlign: 'center' },
  success: { alignItems: 'center', gap: spacing.xs, paddingVertical: spacing.md },
  check: { width: 84, height: 84, borderRadius: 42, alignItems: 'center', justifyContent: 'center', marginBottom: spacing.sm },
  card: { borderRadius: radius.lg, borderWidth: StyleSheet.hairlineWidth, overflow: 'hidden' },
  cardHeader: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, padding: spacing.lg },
  headerRight: { alignItems: 'flex-end' },
  void: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, padding: spacing.md },
  cardBody: { padding: spacing.lg, gap: spacing.sm },
  row: { flexDirection: 'row', alignItems: 'flex-start', gap: spacing.md },
  value: { flexShrink: 1, textAlign: 'right' },
  divider: { height: StyleSheet.hairlineWidth, marginVertical: spacing.xs },
  amount: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  struck: { textDecorationLine: 'line-through' },
  actions: { gap: spacing.sm },
});
