import type { PropsWithChildren } from 'react';
import { StyleSheet, View } from 'react-native';

import { elevation, radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

export function Card({ children }: PropsWithChildren) {
  const { colors } = useTheme();
  return <View style={[styles.card, { backgroundColor: colors.surface }, elevation(colors.shadow)]}>{children}</View>;
}

const styles = StyleSheet.create({
  card: {
    borderRadius: radius.lg,
    padding: spacing.lg,
    gap: spacing.md,
  },
});
