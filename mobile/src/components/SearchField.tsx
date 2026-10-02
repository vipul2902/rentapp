import { useEffect, useState } from 'react';
import { StyleSheet, TextInput, View } from 'react-native';

import { radius, spacing, touchTarget, typography } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

import { Icon } from './Icon';
import { PressableScale } from './PressableScale';

/** Search as you type: reports the trimmed text after a short pause (and at once on submit or clear). */
export function SearchField({
  placeholder,
  onSearch,
  delayMs = 300,
  label = 'Search',
}: {
  placeholder: string;
  onSearch: (term: string) => void;
  delayMs?: number;
  label?: string;
}) {
  const { colors } = useTheme();
  const [text, setText] = useState('');

  useEffect(() => {
    const timer = setTimeout(() => onSearch(text.trim()), delayMs);
    return () => clearTimeout(timer);
  }, [text, delayMs, onSearch]);

  return (
    <View style={[styles.box, { backgroundColor: colors.surface, borderColor: colors.border }]}>
      <Icon name="search" size={18} color={colors.textMuted} />
      <TextInput
        accessibilityLabel={label}
        accessibilityRole="search"
        placeholder={placeholder}
        placeholderTextColor={colors.textMuted}
        value={text}
        onChangeText={setText}
        onSubmitEditing={() => onSearch(text.trim())}
        returnKeyType="search"
        autoCorrect={false}
        style={[styles.input, typography.body, { color: colors.text }]}
      />
      {text ? (
        <PressableScale
          accessibilityRole="button"
          accessibilityLabel="Clear search"
          haptic={false}
          onPress={() => {
            setText('');
            onSearch('');
          }}
          style={styles.clear}
        >
          <Icon name="close-circle" size={18} color={colors.textMuted} />
        </PressableScale>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  box: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, borderWidth: 1, borderRadius: radius.md, paddingHorizontal: spacing.md, minHeight: touchTarget },
  input: { flex: 1, paddingVertical: spacing.sm },
  clear: { width: touchTarget, height: touchTarget, alignItems: 'center', justifyContent: 'center', marginRight: -spacing.md },
});
