import type { ReactNode } from 'react';
import { StyleSheet, View } from 'react-native';

import { spacing, touchTarget } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

import { AppText } from './AppText';
import { Icon, type IconName } from './Icon';
import { PressableScale } from './PressableScale';

interface ListRowProps {
  title: string;
  subtitle?: string;
  /** Leading icon in a tinted square. */
  icon?: IconName;
  /** Custom leading element (e.g. an Avatar); wins over `icon`. */
  leading?: ReactNode;
  trailing?: ReactNode;
  onPress?: () => void;
  accessibilityHint?: string;
}

export function ListRow({ title, subtitle, icon, leading, trailing, onPress, accessibilityHint }: ListRowProps) {
  const { colors } = useTheme();
  const body = (
    <View style={[styles.row, { backgroundColor: colors.surface }]}>
      {leading ?? (icon ? (
        <View style={[styles.icon, { backgroundColor: colors.primarySoft }]}>
          <Icon name={icon} size={20} color={colors.primary} />
        </View>
      ) : null)}
      <View style={styles.text}>
        <AppText variant="heading">{title}</AppText>
        {subtitle ? <AppText muted>{subtitle}</AppText> : null}
      </View>
      {trailing}
      {onPress ? <Icon name="chevron-forward" size={18} color={colors.textMuted} /> : null}
    </View>
  );

  if (!onPress) {
    return body;
  }

  return (
    <PressableScale
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={subtitle ? `${title}, ${subtitle}` : title}
      accessibilityHint={accessibilityHint}
    >
      {body}
    </PressableScale>
  );
}

const styles = StyleSheet.create({
  row: {
    minHeight: touchTarget + spacing.lg,
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.md,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.md,
  },
  icon: { width: 40, height: 40, borderRadius: 12, alignItems: 'center', justifyContent: 'center' },
  text: { flex: 1, gap: 2 },
});
