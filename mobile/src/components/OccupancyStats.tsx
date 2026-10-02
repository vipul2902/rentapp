import { StyleSheet, View } from 'react-native';

import type { BedOccupancy, OccupancySummary } from '@/api/properties';
import { radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

import { AppText } from './AppText';
import type { StatusTone } from './StatusPill';

export const OCCUPANCY_TONE: Record<BedOccupancy, StatusTone> = {
  Vacant: 'success',
  Occupied: 'info',
  Reserved: 'warning',
  Unavailable: 'neutral',
};

/** Compact bed counts: Beds · Occupied · Vacant (+ Reserved / Unavailable only when non-zero). */
export function OccupancyStats({ summary }: { summary: OccupancySummary }) {
  const items: { label: string; value: number; tone?: StatusTone }[] = [
    { label: 'Beds', value: summary.totalBeds },
    { label: 'Occupied', value: summary.occupied, tone: 'info' },
    { label: 'Vacant', value: summary.vacant, tone: 'success' },
  ];
  if (summary.reserved > 0) items.push({ label: 'Reserved', value: summary.reserved, tone: 'warning' });
  if (summary.unavailable > 0) items.push({ label: 'Unavailable', value: summary.unavailable, tone: 'neutral' });

  return (
    <View
      style={styles.row}
      accessible
      accessibilityLabel={items.map((i) => `${i.label} ${i.value}`).join(', ')}
    >
      {items.map((item) => (
        <Stat key={item.label} {...item} />
      ))}
    </View>
  );
}

function Stat({ label, value, tone }: { label: string; value: number; tone?: StatusTone }) {
  const { colors } = useTheme();
  const color = tone === 'success' ? colors.success : tone === 'info' ? colors.info : tone === 'warning' ? colors.warning : colors.text;
  return (
    <View style={[styles.stat, { backgroundColor: colors.surfaceMuted }]}>
      <AppText variant="heading" color={color}>
        {value}
      </AppText>
      <AppText variant="caption" muted>
        {label}
      </AppText>
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  stat: { minWidth: 72, paddingVertical: spacing.sm, paddingHorizontal: spacing.md, borderRadius: radius.md },
});
