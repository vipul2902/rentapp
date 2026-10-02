import DateTimePicker, { DateTimePickerAndroid } from '@react-native-community/datetimepicker';
import { Platform, Pressable, StyleSheet, View } from 'react-native';

import { radius, spacing, touchTarget } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';
import { formatDate, parseIsoDate, toIsoDate } from '@/utils/dates';

import { AppText } from './AppText';
import { TextField } from './TextField';

interface DateFieldProps {
  label: string;
  /** "YYYY-MM-DD". */
  value: string;
  onChange: (value: string) => void;
  minimumDate?: string;
  maximumDate?: string;
  error?: string;
  hint?: string;
}

/** A calendar-date input: native compact picker on iOS, dialog on Android, plain text on the web preview. */
export function DateField({ label, value, onChange, minimumDate, maximumDate, error, hint }: DateFieldProps) {
  const { colors } = useTheme();
  const min = minimumDate ? parseIsoDate(minimumDate) : undefined;
  const max = maximumDate ? parseIsoDate(maximumDate) : undefined;

  if (Platform.OS === 'web') {
    return <TextField label={label} value={value} onChangeText={onChange} error={error} hint={hint ?? 'YYYY-MM-DD'} />;
  }

  const footer = error ? (
    <AppText variant="caption" color={colors.danger} accessibilityLiveRegion="polite">
      {error}
    </AppText>
  ) : hint ? (
    <AppText variant="caption" muted>
      {hint}
    </AppText>
  ) : null;

  if (Platform.OS === 'ios') {
    return (
      <View style={styles.container}>
        <View style={styles.iosRow}>
          <AppText variant="label" style={styles.flex}>
            {label}
          </AppText>
          <DateTimePicker
            accessibilityLabel={label}
            value={parseIsoDate(value)}
            mode="date"
            display="compact"
            minimumDate={min}
            maximumDate={max}
            onValueChange={(_event, date) => onChange(toIsoDate(date))}
          />
        </View>
        {footer}
      </View>
    );
  }

  const open = () =>
    DateTimePickerAndroid.open({
      value: parseIsoDate(value),
      mode: 'date',
      minimumDate: min,
      maximumDate: max,
      onValueChange: (_event, date) => onChange(toIsoDate(date)),
    });

  return (
    <View style={styles.container}>
      <AppText variant="label">{label}</AppText>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={`${label}, ${formatDate(value)}`}
        accessibilityHint="Opens a calendar"
        onPress={open}
        style={[styles.button, { borderColor: error ? colors.danger : colors.border, backgroundColor: colors.surface }]}
      >
        <AppText>{formatDate(value)}</AppText>
      </Pressable>
      {footer}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { gap: spacing.xs },
  flex: { flex: 1 },
  iosRow: { flexDirection: 'row', alignItems: 'center', minHeight: touchTarget, gap: spacing.md },
  button: { minHeight: touchTarget, borderWidth: 1, borderRadius: radius.md, paddingHorizontal: spacing.md, justifyContent: 'center' },
});
