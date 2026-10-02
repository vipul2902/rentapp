import { Platform, type ViewStyle } from 'react-native';

/**
 * Design tokens. Screens and components read colors through `useTheme()` and never hard-code values,
 * so light/dark mode and future brand changes happen in one place.
 *
 * Brand: violet → magenta gradient with a warm orange accent. Status colors are reserved for money
 * states (paid / due / overdue) so they always mean the same thing.
 */

export interface ThemeColors {
  background: string;
  surface: string;
  surfaceMuted: string;
  border: string;
  text: string;
  textMuted: string;
  primary: string;
  primarySoft: string;
  onPrimary: string;
  accent: string;
  accentSoft: string;
  success: string;
  successSurface: string;
  warning: string;
  warningSurface: string;
  danger: string;
  dangerSurface: string;
  info: string;
  infoSurface: string;
  /** Hero gradient, start → end. */
  gradient: readonly [string, string];
  shadow: string;
}

export const lightColors: ThemeColors = {
  background: '#F6F5FB',
  surface: '#FFFFFF',
  surfaceMuted: '#EFEDF7',
  border: '#E1DEEC',
  text: '#16131F',
  textMuted: '#625D73',
  primary: '#5B3DF5',
  primarySoft: '#ECE8FF',
  onPrimary: '#FFFFFF',
  accent: '#F2622E',
  accentSoft: '#FFEDE5',
  success: '#0F8A47',
  successSurface: '#E2F6EA',
  warning: '#9A5B00',
  warningSurface: '#FFF2D9',
  danger: '#C8282E',
  dangerSurface: '#FDE6E6',
  info: '#2F5FD0',
  infoSurface: '#E5EDFD',
  gradient: ['#5B3DF5', '#B23BE8'],
  shadow: '#2A1B6B',
};

export const darkColors: ThemeColors = {
  background: '#0F0D16',
  surface: '#1A1724',
  surfaceMuted: '#241F31',
  border: '#332D44',
  text: '#F3F1F8',
  textMuted: '#ABA4BF',
  primary: '#9A86FF',
  primarySoft: '#2A2446',
  onPrimary: '#0F0D16',
  accent: '#FF8A5C',
  accentSoft: '#3A2219',
  success: '#5CD38D',
  successSurface: '#13301F',
  warning: '#F4B653',
  warningSurface: '#34280F',
  danger: '#FF8B87',
  dangerSurface: '#3B1A1A',
  info: '#86A9FF',
  infoSurface: '#18243F',
  gradient: ['#4B2FD6', '#8E2CC0'],
  shadow: '#000000',
};

export const spacing = { xs: 4, sm: 8, md: 12, lg: 16, xl: 24, xxl: 32 } as const;

export const radius = { sm: 8, md: 12, lg: 18, xl: 24, pill: 999 } as const;

/** Minimum size for anything tappable (Material: 48dp, Apple HIG: 44pt). */
export const touchTarget = 48;

export const typography = {
  display: { fontSize: 32, lineHeight: 38, fontWeight: '800' },
  title: { fontSize: 24, lineHeight: 30, fontWeight: '700' },
  heading: { fontSize: 17, lineHeight: 22, fontWeight: '700' },
  body: { fontSize: 16, lineHeight: 22, fontWeight: '400' },
  label: { fontSize: 14, lineHeight: 20, fontWeight: '600' },
  caption: { fontSize: 13, lineHeight: 18, fontWeight: '400' },
  overline: { fontSize: 12, lineHeight: 16, fontWeight: '700', letterSpacing: 0.6, textTransform: 'uppercase' },
} as const;

export type TypographyVariant = keyof typeof typography;

/** Soft elevated-card shadow, tinted with the brand. */
export function elevation(color: string, level: 1 | 2 = 1): ViewStyle {
  return Platform.select<ViewStyle>({
    ios: { shadowColor: color, shadowOpacity: level === 1 ? 0.08 : 0.16, shadowRadius: level === 1 ? 10 : 18, shadowOffset: { width: 0, height: level === 1 ? 4 : 8 } },
    android: { elevation: level === 1 ? 2 : 6 },
    default: {},
  }) as ViewStyle;
}
