import { Text, type TextProps } from 'react-native';

import { typography, type TypographyVariant } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

interface AppTextProps extends TextProps {
  variant?: TypographyVariant;
  muted?: boolean;
  color?: string;
}

export function AppText({ variant = 'body', muted = false, color, style, ...rest }: AppTextProps) {
  const { colors } = useTheme();
  return (
    <Text
      {...rest}
      style={[typography[variant], { color: color ?? (muted ? colors.textMuted : colors.text) }, style]}
    />
  );
}
