import { LinearGradient } from 'expo-linear-gradient';
import { type PropsWithChildren, useEffect, useState } from 'react';
import { Animated, ScrollView, StyleSheet, View } from 'react-native';

import { elevation, radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

import { AppText } from './AppText';
import { Button } from './Button';
import { Icon, type IconName } from './Icon';
import { PressableScale } from './PressableScale';

/** The colorful hero panel at the top of Home and detail screens. */
export function GradientHero({ children }: PropsWithChildren) {
  const { colors } = useTheme();
  return (
    <LinearGradient colors={colors.gradient} start={{ x: 0, y: 0 }} end={{ x: 1, y: 1 }} style={[styles.hero, elevation(colors.primary, 2)]}>
      {/* Soft decorative circles for depth. */}
      <View style={[styles.blob, { top: -40, right: -30, width: 160, height: 160 }]} />
      <View style={[styles.blob, { bottom: -50, left: -20, width: 120, height: 120 }]} />
      {children}
    </LinearGradient>
  );
}

type Tone = 'primary' | 'success' | 'warning' | 'danger' | 'info' | 'accent';

export function useToneColors(tone: Tone) {
  const { colors } = useTheme();
  return {
    primary: { fg: colors.primary, bg: colors.primarySoft },
    success: { fg: colors.success, bg: colors.successSurface },
    warning: { fg: colors.warning, bg: colors.warningSurface },
    danger: { fg: colors.danger, bg: colors.dangerSurface },
    info: { fg: colors.info, bg: colors.infoSurface },
    accent: { fg: colors.accent, bg: colors.accentSoft },
  }[tone];
}

/** A small number tile with an icon, e.g. "12 · Vacant beds". */
export function StatTile({ icon, value, label, tone = 'primary', onPress }: { icon: IconName; value: string; label: string; tone?: Tone; onPress?: () => void }) {
  const { colors } = useTheme();
  const tint = useToneColors(tone);
  const body = (
    <View style={[styles.tile, { backgroundColor: colors.surface }, elevation(colors.shadow)]}>
      <View style={[styles.tileIcon, { backgroundColor: tint.bg }]}>
        <Icon name={icon} size={18} color={tint.fg} />
      </View>
      <AppText variant="heading" style={styles.tileValue}>
        {value}
      </AppText>
      <AppText variant="caption" muted numberOfLines={1}>
        {label}
      </AppText>
    </View>
  );
  return onPress ? (
    <PressableScale accessibilityRole="button" accessibilityLabel={`${label}: ${value}`} onPress={onPress} style={styles.tileWrap}>
      {body}
    </PressableScale>
  ) : (
    <View style={styles.tileWrap} accessible accessibilityLabel={`${label}: ${value}`}>
      {body}
    </View>
  );
}

/** Horizontal filter chips with optional counts. */
export function ChipBar<T extends string>({
  options,
  value,
  onChange,
  label,
}: {
  options: readonly { value: T; label: string; count?: number; icon?: IconName }[];
  value: T;
  onChange: (value: T) => void;
  label: string;
}) {
  const { colors } = useTheme();
  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.chips} accessibilityRole="tablist" accessibilityLabel={label}>
      {options.map((option) => {
        const selected = option.value === value;
        return (
          <PressableScale
            key={option.value}
            accessibilityRole="tab"
            accessibilityState={{ selected }}
            accessibilityLabel={option.count === undefined ? option.label : `${option.label}, ${option.count}`}
            onPress={() => onChange(option.value)}
            style={[styles.chip, { backgroundColor: selected ? colors.primary : colors.surface, borderColor: selected ? colors.primary : colors.border }]}
          >
            {option.icon ? <Icon name={option.icon} size={15} color={selected ? colors.onPrimary : colors.textMuted} /> : null}
            <AppText variant="label" color={selected ? colors.onPrimary : colors.text}>
              {option.label}
            </AppText>
            {option.count !== undefined ? (
              <View style={[styles.count, { backgroundColor: selected ? 'rgba(255,255,255,0.25)' : colors.surfaceMuted }]}>
                <AppText variant="caption" color={selected ? colors.onPrimary : colors.textMuted}>
                  {option.count}
                </AppText>
              </View>
            ) : null}
          </PressableScale>
        );
      })}
    </ScrollView>
  );
}

/** A friendly empty state: big icon, message and an optional action. */
export function EmptyState({ icon, title, message, actionLabel, onAction }: { icon: IconName; title: string; message?: string; actionLabel?: string; onAction?: () => void }) {
  const { colors } = useTheme();
  return (
    <View style={styles.empty}>
      <View style={[styles.emptyIcon, { backgroundColor: colors.primarySoft }]}>
        <Icon name={icon} size={34} color={colors.primary} />
      </View>
      <AppText variant="heading" style={styles.center}>
        {title}
      </AppText>
      {message ? (
        <AppText muted style={styles.center}>
          {message}
        </AppText>
      ) : null}
      {actionLabel && onAction ? (
        <View style={styles.emptyAction}>
          <Button label={actionLabel} onPress={onAction} />
        </View>
      ) : null}
    </View>
  );
}

/** Section title with an optional "See all" link. */
export function SectionHeader({ title, actionLabel, onAction }: { title: string; actionLabel?: string; onAction?: () => void }) {
  const { colors } = useTheme();
  return (
    <View style={styles.section}>
      <AppText variant="heading">{title}</AppText>
      {actionLabel && onAction ? (
        <PressableScale accessibilityRole="button" accessibilityLabel={actionLabel} onPress={onAction} haptic={false} style={styles.sectionAction}>
          <AppText variant="label" color={colors.primary}>
            {actionLabel}
          </AppText>
          <Icon name="chevron-forward" size={16} color={colors.primary} />
        </PressableScale>
      ) : null}
    </View>
  );
}

/** Pulsing placeholder blocks while data loads (feels faster than a spinner). */
export function SkeletonList({ rows = 4 }: { rows?: number }) {
  const { colors } = useTheme();
  const [pulse] = useState(() => new Animated.Value(0.5));
  useEffect(() => {
    const loop = Animated.loop(
      Animated.sequence([
        Animated.timing(pulse, { toValue: 1, duration: 650, useNativeDriver: true }),
        Animated.timing(pulse, { toValue: 0.5, duration: 650, useNativeDriver: true }),
      ]),
    );
    loop.start();
    return () => loop.stop();
  }, [pulse]);

  return (
    <View style={styles.skeleton} accessibilityRole="progressbar" accessibilityLabel="Loading">
      {Array.from({ length: rows }, (_, i) => (
        <Animated.View key={i} style={[styles.skeletonRow, { backgroundColor: colors.surface, opacity: pulse }]}>
          <View style={[styles.skeletonCircle, { backgroundColor: colors.surfaceMuted }]} />
          <View style={styles.flex}>
            <View style={[styles.skeletonLine, { width: '60%', backgroundColor: colors.surfaceMuted }]} />
            <View style={[styles.skeletonLine, { width: '35%', backgroundColor: colors.surfaceMuted }]} />
          </View>
        </Animated.View>
      ))}
    </View>
  );
}

/** Occupied share of beds as a thin bar. */
export function ProgressBar({ value, total, color }: { value: number; total: number; color?: string }) {
  const { colors } = useTheme();
  const share = total > 0 ? Math.min(1, value / total) : 0;
  return (
    <View style={[styles.bar, { backgroundColor: colors.surfaceMuted }]} accessibilityElementsHidden importantForAccessibility="no-hide-descendants">
      <View style={[styles.barFill, { width: `${share * 100}%`, backgroundColor: color ?? colors.primary }]} />
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  center: { textAlign: 'center' },
  hero: { borderRadius: radius.xl, padding: spacing.xl, gap: spacing.sm, overflow: 'hidden' },
  blob: { position: 'absolute', borderRadius: 999, backgroundColor: 'rgba(255,255,255,0.10)' },
  tileWrap: { flex: 1, minWidth: 140 },
  tile: { borderRadius: radius.lg, padding: spacing.lg, gap: spacing.xs },
  tileIcon: { width: 34, height: 34, borderRadius: 10, alignItems: 'center', justifyContent: 'center', marginBottom: spacing.xs },
  tileValue: { fontSize: 20, lineHeight: 26 },
  chips: { gap: spacing.sm, paddingVertical: spacing.xs },
  chip: { flexDirection: 'row', alignItems: 'center', gap: spacing.xs, minHeight: 40, paddingHorizontal: spacing.md, borderRadius: radius.pill, borderWidth: 1 },
  count: { minWidth: 22, paddingHorizontal: 6, borderRadius: radius.pill, alignItems: 'center' },
  empty: { alignItems: 'center', gap: spacing.sm, paddingVertical: spacing.xxl, paddingHorizontal: spacing.lg },
  emptyIcon: { width: 76, height: 76, borderRadius: 38, alignItems: 'center', justifyContent: 'center', marginBottom: spacing.sm },
  emptyAction: { alignSelf: 'stretch', marginTop: spacing.md },
  section: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginTop: spacing.sm },
  sectionAction: { flexDirection: 'row', alignItems: 'center', gap: 2, minHeight: 36, paddingHorizontal: spacing.xs },
  skeleton: { gap: spacing.md },
  skeletonRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, padding: spacing.lg, borderRadius: radius.lg },
  skeletonCircle: { width: 44, height: 44, borderRadius: 22 },
  skeletonLine: { height: 12, borderRadius: 6, marginVertical: 4 },
  bar: { height: 8, borderRadius: 4, overflow: 'hidden' },
  barFill: { height: 8, borderRadius: 4 },
});
