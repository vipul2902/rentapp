/**
 * Design tokens. Screens and components read colors through `useTheme()` and never hard-code values,
 * so light/dark mode and future brand changes happen in one place.
 */

export interface ThemeColors {
  background: string;
  surface: string;
  surfaceMuted: string;
  border: string;
  text: string;
  textMuted: string;
  primary: string;
  onPrimary: string;
  success: string;
  successSurface: string;
  warning: string;
  warningSurface: string;
  danger: string;
  dangerSurface: string;
  info: string;
  infoSurface: string;
}

export const lightColors: ThemeColors = {
  background: '#F6F7F9',
  surface: '#FFFFFF',
  surfaceMuted: '#EEF0F3',
  border: '#DADDE3',
  text: '#14171C',
  textMuted: '#5B6270',
  primary: '#1F5FD1',
  onPrimary: '#FFFFFF',
  success: '#17733A',
  successSurface: '#E3F4E8',
  warning: '#8A5A00',
  warningSurface: '#FCF0D6',
  danger: '#B42318',
  dangerSurface: '#FDE7E5',
  info: '#1F5FD1',
  infoSurface: '#E5EDFB',
};

export const darkColors: ThemeColors = {
  background: '#0E1013',
  surface: '#171A1F',
  surfaceMuted: '#20242B',
  border: '#2C313A',
  text: '#F1F3F6',
  textMuted: '#A3AAB7',
  primary: '#6E9CF5',
  onPrimary: '#0E1013',
  success: '#5FCB85',
  successSurface: '#15301F',
  warning: '#F0B54A',
  warningSurface: '#33270F',
  danger: '#F2867C',
  dangerSurface: '#3A1A17',
  info: '#6E9CF5',
  infoSurface: '#18243A',
};

export const spacing = { xs: 4, sm: 8, md: 12, lg: 16, xl: 24, xxl: 32 } as const;

export const radius = { sm: 6, md: 10, lg: 16, pill: 999 } as const;

/** Minimum size for anything tappable (Material: 48dp, Apple HIG: 44pt). */
export const touchTarget = 48;

export const typography = {
  title: { fontSize: 24, lineHeight: 30, fontWeight: '700' },
  heading: { fontSize: 18, lineHeight: 24, fontWeight: '600' },
  body: { fontSize: 16, lineHeight: 22, fontWeight: '400' },
  label: { fontSize: 14, lineHeight: 20, fontWeight: '600' },
  caption: { fontSize: 13, lineHeight: 18, fontWeight: '400' },
} as const;

export type TypographyVariant = keyof typeof typography;
