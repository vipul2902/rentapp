import { Pressable, StyleSheet, View } from 'react-native';

import { radius, spacing, touchTarget } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

import { AppText } from './AppText';

interface SegmentedControlProps<T extends string> {
  label: string;
  options: readonly { value: T; label: string }[];
  value: T;
  onChange: (value: T) => void;
  disabled?: boolean;
}

export function SegmentedControl<T extends string>({ label, options, value, onChange, disabled = false }: SegmentedControlProps<T>) {
  const { colors } = useTheme();
  return (
    <View style={styles.container}>
      <AppText variant="label">{label}</AppText>
      <View style={[styles.track, { borderColor: colors.border }]} accessibilityRole="radiogroup" accessibilityLabel={label}>
        {options.map((option) => {
          const selected = option.value === value;
          return (
            <Pressable
              key={option.value}
              accessibilityRole="radio"
              accessibilityLabel={option.label}
              accessibilityState={{ selected, disabled }}
              disabled={disabled}
              onPress={() => onChange(option.value)}
              style={[styles.segment, { backgroundColor: selected ? colors.primary : colors.surface }]}
            >
              <AppText variant="label" color={selected ? colors.onPrimary : colors.text}>
                {option.label}
              </AppText>
            </Pressable>
          );
        })}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { gap: spacing.xs },
  track: { flexDirection: 'row', borderWidth: 1, borderRadius: radius.md, overflow: 'hidden' },
  segment: { flex: 1, minHeight: touchTarget, alignItems: 'center', justifyContent: 'center', paddingHorizontal: spacing.sm },
});
