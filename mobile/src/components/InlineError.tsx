import { StyleSheet, View } from 'react-native';

import { userMessageFor } from '@/api/errors';
import { radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

import { AppText } from './AppText';

/** Error banner for a failed form submission. Renders nothing when there is no error. */
export function InlineError({ error, action }: { error: unknown; action: string }) {
  const { colors } = useTheme();
  if (!error) {
    return null;
  }
  const { title, detail } = userMessageFor(error, action);
  return (
    <View style={[styles.box, { backgroundColor: colors.dangerSurface }]} accessibilityRole="alert">
      <AppText variant="label" color={colors.danger}>
        {title}
      </AppText>
      <AppText color={colors.danger}>{detail}</AppText>
    </View>
  );
}

const styles = StyleSheet.create({
  box: { borderRadius: radius.md, padding: spacing.md, gap: spacing.xs },
});
