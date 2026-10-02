import { useColorScheme } from 'react-native';

import { darkColors, lightColors, type ThemeColors } from './tokens';

export function useTheme(): { colors: ThemeColors; isDark: boolean } {
  const isDark = useColorScheme() === 'dark';
  return { colors: isDark ? darkColors : lightColors, isDark };
}
