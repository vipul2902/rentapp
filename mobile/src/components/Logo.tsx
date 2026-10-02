import { LinearGradient } from 'expo-linear-gradient';
import { StyleSheet, View } from 'react-native';

import { useTheme } from '@/theme/useTheme';

import { AppText } from './AppText';
import { Icon } from './Icon';

/** Product name in one place, so renaming the app is a one-line change. */
export const APP_NAME = 'RentApp';

/** The RentApp mark: a home with a rupee badge, on the brand gradient. */
export function LogoMark({ size = 56 }: { size?: number }) {
  const { colors } = useTheme();
  return (
    <LinearGradient
      colors={colors.gradient}
      start={{ x: 0, y: 0 }}
      end={{ x: 1, y: 1 }}
      style={[styles.mark, { width: size, height: size, borderRadius: size * 0.28 }]}
      accessibilityElementsHidden
      importantForAccessibility="no-hide-descendants"
    >
      <Icon name="home" size={size * 0.52} color="#FFFFFF" />
      <View style={[styles.badge, { width: size * 0.38, height: size * 0.38, borderRadius: size * 0.19, right: size * 0.1, bottom: size * 0.1, backgroundColor: colors.accent }]}>
        <AppText variant="label" color="#FFFFFF" style={{ fontSize: size * 0.22, lineHeight: size * 0.28 }}>
          ₹
        </AppText>
      </View>
    </LinearGradient>
  );
}

export function Logo({ size = 56, tagline }: { size?: number; tagline?: string }) {
  return (
    <View style={styles.row} accessible accessibilityRole="header" accessibilityLabel={APP_NAME}>
      <LogoMark size={size} />
      <View>
        <AppText variant="title" style={{ fontSize: size * 0.48, lineHeight: size * 0.58 }}>
          {APP_NAME}
        </AppText>
        {tagline ? (
          <AppText variant="caption" muted>
            {tagline}
          </AppText>
        ) : null}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  mark: { alignItems: 'center', justifyContent: 'center' },
  badge: { position: 'absolute', alignItems: 'center', justifyContent: 'center', borderWidth: 2, borderColor: '#FFFFFF' },
});
