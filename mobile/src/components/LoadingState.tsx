import { ActivityIndicator, StyleSheet, View } from 'react-native';

import { spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

import { AppText } from './AppText';

export function LoadingState({ message = 'Loading…' }: { message?: string }) {
  const { colors } = useTheme();
  return (
    <View style={styles.container} accessibilityRole="progressbar" accessibilityLabel={message}>
      <ActivityIndicator size="large" color={colors.primary} />
      <AppText muted>{message}</AppText>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { alignItems: 'center', justifyContent: 'center', gap: spacing.md, paddingVertical: spacing.xxl },
});
