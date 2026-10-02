import { StyleSheet, Switch, View } from 'react-native';

import { spacing, touchTarget } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

import { AppText } from './AppText';

interface ToggleRowProps {
  label: string;
  description?: string;
  value: boolean;
  onChange: (value: boolean) => void;
}

export function ToggleRow({ label, description, value, onChange }: ToggleRowProps) {
  const { colors } = useTheme();
  return (
    <View style={styles.row}>
      <View style={styles.text}>
        <AppText variant="label">{label}</AppText>
        {description ? (
          <AppText variant="caption" muted>
            {description}
          </AppText>
        ) : null}
      </View>
      <Switch accessibilityLabel={label} value={value} onValueChange={onChange} trackColor={{ true: colors.primary }} />
    </View>
  );
}

const styles = StyleSheet.create({
  row: { minHeight: touchTarget, flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  text: { flex: 1 },
});
