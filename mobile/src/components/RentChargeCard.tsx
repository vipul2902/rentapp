import { router } from 'expo-router';
import { StyleSheet, View } from 'react-native';

import type { RentCharge } from '@/api/rent';
import { periodLabel, RENT_STATUS } from '@/hooks/useRent';
import { elevation, radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';
import { formatRupees } from '@/utils/money';

import { AppText } from './AppText';
import { Avatar } from './Avatar';
import { FadeIn } from './FadeIn';
import { PressableScale } from './PressableScale';
import { StatusPill } from './StatusPill';

/** One month's rent for one tenant: who, which month, how much is left, and its status. */
export function RentChargeCard({ charge, index = 0 }: { charge: RentCharge; index?: number }) {
  const { colors } = useTheme();
  const status = RENT_STATUS[charge.status];
  const settled = charge.balance <= 0;
  const amount = formatRupees(settled ? charge.amount : charge.balance);
  const place = `${periodLabel(charge.periodStart)} · Room ${charge.roomNumber} · Bed ${charge.bedLabel}`;

  return (
    <FadeIn delay={Math.min(index, 8) * 40}>
      <PressableScale
        accessibilityRole="button"
        accessibilityLabel={`${charge.tenantName}, ${periodLabel(charge.periodStart)}, ${amount}, ${status.label}`}
        onPress={() => router.push({ pathname: '/rent/[id]', params: { id: charge.id } })}
        style={[styles.card, { backgroundColor: colors.surface }, elevation(colors.shadow)]}
      >
        <Avatar name={charge.tenantName} />
        <View style={styles.middle}>
          <AppText variant="heading" numberOfLines={1}>
            {charge.tenantName}
          </AppText>
          <AppText variant="caption" muted numberOfLines={1}>
            {place}
          </AppText>
          {charge.daysOverdue > 0 ? (
            <AppText variant="caption" color={colors.danger}>
              {charge.daysOverdue === 1 ? '1 day late' : `${charge.daysOverdue} days late`}
            </AppText>
          ) : charge.paidAmount > 0 && !settled ? (
            <AppText variant="caption" muted>
              {formatRupees(charge.paidAmount)} paid of {formatRupees(charge.amount)}
            </AppText>
          ) : null}
        </View>
        <View style={styles.right}>
          <AppText variant="heading" color={charge.status === 'Overdue' ? colors.danger : colors.text}>
            {amount}
          </AppText>
          <StatusPill label={status.label} tone={status.tone} icon={status.icon} />
        </View>
      </PressableScale>
    </FadeIn>
  );
}

const styles = StyleSheet.create({
  card: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, padding: spacing.lg, borderRadius: radius.lg },
  middle: { flex: 1, gap: 2 },
  right: { alignItems: 'flex-end', gap: spacing.xs },
});
