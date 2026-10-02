import { StyleSheet, Switch, View } from 'react-native';

import type { StaffPermission } from '@/api/types';
import { STAFF_PERMISSIONS } from '@/auth/permissions';
import { spacing, touchTarget } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

import { AppText } from './AppText';

interface PermissionTogglesProps {
  value: readonly StaffPermission[];
  onChange: (next: StaffPermission[]) => void;
  disabled?: boolean;
}

export function PermissionToggles({ value, onChange, disabled = false }: PermissionTogglesProps) {
  const { colors } = useTheme();

  const toggle = (key: StaffPermission, enabled: boolean) =>
    onChange(STAFF_PERMISSIONS.map((p) => p.key).filter((k) => (k === key ? enabled : value.includes(k))));

  return (
    <View style={styles.list}>
      {STAFF_PERMISSIONS.map((permission) => (
        <View key={permission.key} style={styles.row}>
          <View style={styles.text}>
            <AppText variant="label">{permission.label}</AppText>
            <AppText variant="caption" muted>
              {permission.description}
            </AppText>
          </View>
          <Switch
            accessibilityLabel={permission.label}
            value={value.includes(permission.key)}
            onValueChange={(enabled) => toggle(permission.key, enabled)}
            disabled={disabled}
            trackColor={{ true: colors.primary }}
          />
        </View>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  list: { gap: spacing.sm },
  row: { minHeight: touchTarget, flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  text: { flex: 1 },
});
