import type { ReactNode } from 'react';
import { Pressable, StyleSheet, View } from 'react-native';

import { spacing, touchTarget } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

import { AppText } from './AppText';

interface ListRowProps {
  title: string;
  subtitle?: string;
  trailing?: ReactNode;
  onPress?: () => void;
  accessibilityHint?: string;
}

export function ListRow({ title, subtitle, trailing, onPress, accessibilityHint }: ListRowProps) {
  const { colors } = useTheme();
  return (
    <Pressable
      onPress={onPress}
      disabled={!onPress}
      accessibilityRole={onPress ? 'button' : undefined}
      accessibilityLabel={subtitle ? `${title}, ${subtitle}` : title}
      accessibilityHint={accessibilityHint}
      style={({ pressed }) => [styles.row, { backgroundColor: pressed ? colors.surfaceMuted : colors.surface }]}
    >
      <View style={styles.text}>
        <AppText variant="heading">{title}</AppText>
        {subtitle ? <AppText muted>{subtitle}</AppText> : null}
      </View>
      {trailing}
      {onPress ? (
        <AppText muted accessibilityElementsHidden importantForAccessibility="no">
          ›
        </AppText>
      ) : null}
    </Pressable>
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
  text: { flex: 1, gap: spacing.xs },
});
