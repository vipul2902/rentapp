import { useState } from 'react';
import { StyleSheet, View } from 'react-native';

import { ApiClientError } from '@/api/errors';
import { ErrorState } from '@/components/ErrorState';
import { spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

/** Shown at start-up when a saved session exists but the server cannot be reached to resume it. */
export function ConnectionErrorScreen({ onRetry }: { onRetry: () => Promise<void> }) {
  const { colors } = useTheme();
  const [retrying, setRetrying] = useState(false);

  const retry = async () => {
    setRetrying(true);
    try {
      await onRetry();
    } finally {
      setRetrying(false);
    }
  };

  return (
    <View style={[styles.container, { backgroundColor: colors.background }]}>
      <ErrorState
        error={new ApiClientError({ kind: 'network' })}
        onRetry={() => void retry()}
        retrying={retrying}
        hint="Your data is safe. We will continue where you left off once the connection is back."
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, justifyContent: 'center', padding: spacing.xl },
});
