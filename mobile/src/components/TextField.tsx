import { forwardRef } from 'react';
import { StyleSheet, TextInput, type TextInputProps, View } from 'react-native';

import { radius, spacing, touchTarget, typography } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

import { AppText } from './AppText';

interface TextFieldProps extends Omit<TextInputProps, 'style'> {
  label: string;
  error?: string;
  hint?: string;
}

export const TextField = forwardRef<TextInput, TextFieldProps>(function TextField({ label, error, hint, ...input }, ref) {
  const { colors } = useTheme();
  return (
    <View style={styles.container}>
      <AppText variant="label">{label}</AppText>
      <TextInput
        ref={ref}
        accessibilityLabel={label}
        accessibilityHint={error ?? hint}
        placeholderTextColor={colors.textMuted}
        {...input}
        style={[
          styles.input,
          typography.body,
          { color: colors.text, backgroundColor: colors.surface, borderColor: error ? colors.danger : colors.border },
        ]}
      />
      {error ? (
        <AppText variant="caption" color={colors.danger} accessibilityLiveRegion="polite">
          {error}
        </AppText>
      ) : hint ? (
        <AppText variant="caption" muted>
          {hint}
        </AppText>
      ) : null}
    </View>
  );
});

const styles = StyleSheet.create({
  container: { gap: spacing.xs },
  input: {
    minHeight: touchTarget,
    borderWidth: 1,
    borderRadius: radius.md,
    paddingHorizontal: spacing.md,
  },
});
