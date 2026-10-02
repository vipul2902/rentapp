import { LinearGradient } from 'expo-linear-gradient';
import { ActivityIndicator, StyleSheet, View } from 'react-native';

import { elevation, radius, spacing, touchTarget } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

import { AppText } from './AppText';
import { Icon, type IconName } from './Icon';
import { PressableScale } from './PressableScale';

interface ButtonProps {
  label: string;
  onPress: () => void;
  variant?: 'primary' | 'secondary' | 'danger';
  icon?: IconName;
  loading?: boolean;
  disabled?: boolean;
  accessibilityHint?: string;
}

/** Large, full-width action. Primary actions (e.g. "Record Payment") must be obvious and easy to hit. */
export function Button({ label, onPress, variant = 'primary', icon, loading = false, disabled = false, accessibilityHint }: ButtonProps) {
  const { colors } = useTheme();
  const inactive = disabled || loading;
  const foreground = variant === 'primary' ? colors.onPrimary : variant === 'danger' ? colors.danger : colors.primary;

  const content = loading ? (
    <ActivityIndicator color={foreground} />
  ) : (
    <View style={styles.row}>
      {icon ? <Icon name={icon} size={18} color={foreground} /> : null}
      <AppText variant="label" color={foreground}>
        {label}
      </AppText>
    </View>
  );

  return (
    <PressableScale
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityHint={accessibilityHint}
      accessibilityState={{ disabled: inactive, busy: loading }}
      disabled={inactive}
      onPress={onPress}
      style={[{ opacity: inactive ? 0.5 : 1 }, variant === 'primary' ? elevation(colors.primary, 2) : null, styles.shape]}
    >
      {variant === 'primary' ? (
        <LinearGradient colors={colors.gradient} start={{ x: 0, y: 0 }} end={{ x: 1, y: 1 }} style={[styles.base, styles.shape]}>
          {content}
        </LinearGradient>
      ) : (
        <View
          style={[
            styles.base,
            styles.shape,
            {
              backgroundColor: variant === 'danger' ? colors.dangerSurface : colors.primarySoft,
            },
          ]}
        >
          {content}
        </View>
      )}
    </PressableScale>
  );
}

const styles = StyleSheet.create({
  shape: { borderRadius: radius.md },
  base: { minHeight: touchTarget + 4, paddingHorizontal: spacing.lg, alignItems: 'center', justifyContent: 'center' },
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
});
