import { ActivityIndicator, Pressable, StyleSheet } from 'react-native';

import { radius, spacing, touchTarget } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

import { AppText } from './AppText';

interface ButtonProps {
  label: string;
  onPress: () => void;
  variant?: 'primary' | 'secondary';
  loading?: boolean;
  disabled?: boolean;
  accessibilityHint?: string;
}

/** Large, full-width action. Primary actions (e.g. "Record Payment") must be obvious and easy to hit. */
export function Button({ label, onPress, variant = 'primary', loading = false, disabled = false, accessibilityHint }: ButtonProps) {
  const { colors } = useTheme();
  const isPrimary = variant === 'primary';
  const inactive = disabled || loading;
  const foreground = isPrimary ? colors.onPrimary : colors.primary;

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityHint={accessibilityHint}
      accessibilityState={{ disabled: inactive, busy: loading }}
      disabled={inactive}
      onPress={onPress}
      style={({ pressed }) => [
        styles.base,
        {
          backgroundColor: isPrimary ? colors.primary : colors.surface,
          borderColor: colors.primary,
          opacity: inactive ? 0.5 : pressed ? 0.85 : 1,
        },
      ]}
    >
      {loading ? <ActivityIndicator color={foreground} /> : <AppText variant="label" color={foreground}>{label}</AppText>}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  base: {
    minHeight: touchTarget,
    borderRadius: radius.md,
    borderWidth: 1,
    paddingHorizontal: spacing.lg,
    alignItems: 'center',
    justifyContent: 'center',
  },
});
