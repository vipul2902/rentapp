import type { PropsWithChildren, ReactElement } from 'react';
import { KeyboardAvoidingView, Platform, type RefreshControlProps, ScrollView, StyleSheet } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

interface FormScreenProps extends PropsWithChildren {
  refreshControl?: ReactElement<RefreshControlProps>;
}

/** Scrollable screen body that keeps inputs visible above the keyboard. */
export function FormScreen({ children, refreshControl }: FormScreenProps) {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  return (
    <KeyboardAvoidingView style={styles.flex} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <ScrollView
        style={[styles.flex, { backgroundColor: colors.background }]}
        contentContainerStyle={[styles.content, { paddingBottom: insets.bottom + spacing.xl }]}
        keyboardShouldPersistTaps="handled"
        refreshControl={refreshControl}
      >
        {children}
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  content: { padding: spacing.lg, gap: spacing.lg },
});
