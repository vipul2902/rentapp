import { StyleSheet, View } from 'react-native';

import { userMessageFor } from '@/api/errors';
import { spacing } from '@/theme/tokens';

import { AppText } from './AppText';
import { Button } from './Button';

interface ErrorStateProps {
  error: unknown;
  /** What the user was trying to do, e.g. "load tenants". */
  action?: string;
  onRetry?: () => void;
  retrying?: boolean;
  hint?: string;
}

export function ErrorState({ error, action, onRetry, retrying = false, hint }: ErrorStateProps) {
  const { title, detail } = userMessageFor(error, action);
  return (
    <View style={styles.container} accessibilityRole="alert">
      <AppText variant="heading">{title}</AppText>
      <AppText muted>{detail}</AppText>
      {hint ? <AppText variant="caption" muted>{hint}</AppText> : null}
      {onRetry ? <Button label="Try again" variant="secondary" onPress={onRetry} loading={retrying} /> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { gap: spacing.sm, paddingVertical: spacing.lg },
});
