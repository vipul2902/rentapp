import { StyleSheet, View } from 'react-native';

import { AppText } from './AppText';

// Friendly, distinct pastel pairs (background, text). The same name always gets the same colors.
const PAIRS: readonly (readonly [string, string])[] = [
  ['#ECE8FF', '#4A2FD6'],
  ['#FFE9E0', '#B8441A'],
  ['#DFF5EA', '#0C7A3E'],
  ['#E3EEFF', '#2554C2'],
  ['#FFF1D6', '#8C5300'],
  ['#FBE4F3', '#A1266F'],
  ['#E2F6F8', '#0F6E7A'],
];

export function initials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  const first = parts[0]?.[0] ?? '?';
  const last = parts.length > 1 ? (parts[parts.length - 1]?.[0] ?? '') : '';
  return (first + last).toUpperCase();
}

function pairFor(name: string): readonly [string, string] {
  let hash = 0;
  for (const ch of name) hash = (hash * 31 + ch.charCodeAt(0)) >>> 0;
  return PAIRS[hash % PAIRS.length] ?? PAIRS[0]!;
}

/** Initials in a colored circle; decorative (the name is always shown next to it). */
export function Avatar({ name, size = 44 }: { name: string; size?: number }) {
  const [bg, fg] = pairFor(name);
  return (
    <View
      accessibilityElementsHidden
      importantForAccessibility="no-hide-descendants"
      style={[styles.circle, { width: size, height: size, borderRadius: size / 2, backgroundColor: bg }]}
    >
      <AppText variant="label" color={fg} style={{ fontSize: size * 0.36, lineHeight: size * 0.44 }}>
        {initials(name)}
      </AppText>
    </View>
  );
}

const styles = StyleSheet.create({
  circle: { alignItems: 'center', justifyContent: 'center' },
});
