import { StyleSheet, View } from 'react-native';

import { radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

import { AppText } from './AppText';
import { Icon, type IconName } from './Icon';

export type StatusTone = 'success' | 'warning' | 'danger' | 'info' | 'neutral';

interface StatusPillProps {
  label: string;
  tone: StatusTone;
  icon?: IconName;
}

/**
 * Colored status label. Color is never the only signal: the text always states the status,
 * which matters for color-blind users reading Paid / Partially Paid / Due / Overdue.
 */
export function StatusPill({ label, tone, icon }: StatusPillProps) {
  const { colors } = useTheme();
  const palette = {
    success: { fg: colors.success, bg: colors.successSurface },
    warning: { fg: colors.warning, bg: colors.warningSurface },
    danger: { fg: colors.danger, bg: colors.dangerSurface },
    info: { fg: colors.info, bg: colors.infoSurface },
    neutral: { fg: colors.textMuted, bg: colors.surfaceMuted },
  }[tone];

  return (
    <View style={[styles.pill, { backgroundColor: palette.bg }]}>
      {icon ? <Icon name={icon} size={13} color={palette.fg} /> : null}
      <AppText variant="label" color={palette.fg} style={styles.text}>
        {label}
      </AppText>
    </View>
  );
}

const styles = StyleSheet.create({
  pill: {
    alignSelf: 'flex-start',
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    borderRadius: radius.pill,
    paddingHorizontal: spacing.md,
    paddingVertical: 4,
  },
  text: { fontSize: 13 },
});
