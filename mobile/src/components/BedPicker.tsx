import { Pressable, StyleSheet, View } from 'react-native';

import type { VacantBed } from '@/hooks/useTenants';
import { useVacantBeds } from '@/hooks/useTenants';
import { radius, spacing, touchTarget } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';
import { formatRupees } from '@/utils/money';

import { AppText } from './AppText';
import { ErrorState } from './ErrorState';
import { LoadingState } from './LoadingState';

interface BedPickerProps {
  value?: string;
  onChange: (bed: VacantBed) => void;
  /** Limit to one property. */
  propertyId?: string;
  /** Hide this bed (e.g. the tenant's current bed when moving). */
  excludeBedId?: string;
  error?: string;
}

/** Lists every bed a tenant can move into right now, grouped by property and room. */
export function BedPicker({ value, onChange, propertyId, excludeBedId, error }: BedPickerProps) {
  const { colors } = useTheme();
  const { beds, isPending, error: loadError, refetch } = useVacantBeds(propertyId);
  const options = beds.filter((b) => b.bedId !== excludeBedId);

  if (isPending) return <LoadingState message="Finding vacant beds…" />;
  if (loadError) return <ErrorState error={loadError} action="load beds" onRetry={refetch} />;

  return (
    <View style={styles.container} accessibilityRole="radiogroup" accessibilityLabel="Bed">
      <AppText variant="label">Bed</AppText>
      {options.length === 0 ? (
        <AppText muted>No vacant beds. Add a room or bed, or move someone out first.</AppText>
      ) : (
        options.map((bed) => {
          const selected = bed.bedId === value;
          const place = `${bed.propertyName} · Room ${bed.roomNumber} · Bed ${bed.label}`;
          return (
            <Pressable
              key={bed.bedId}
              accessibilityRole="radio"
              accessibilityState={{ selected }}
              accessibilityLabel={place}
              onPress={() => onChange(bed)}
              style={[
                styles.option,
                { borderColor: selected ? colors.primary : colors.border, backgroundColor: selected ? colors.infoSurface : colors.surface },
              ]}
            >
              <AppText style={styles.flex}>{place}</AppText>
              {bed.defaultMonthlyRent !== null ? <AppText muted>{formatRupees(bed.defaultMonthlyRent)}</AppText> : null}
            </Pressable>
          );
        })
      )}
      {error ? (
        <AppText variant="caption" color={colors.danger}>
          {error}
        </AppText>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { gap: spacing.sm },
  flex: { flex: 1 },
  option: {
    minHeight: touchTarget,
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.md,
    borderWidth: 1,
    borderRadius: radius.md,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
  },
});
