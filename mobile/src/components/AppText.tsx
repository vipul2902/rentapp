import { Text, type TextProps } from 'react-native';

import { typography, type TypographyVariant } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

interface AppTextProps extends TextProps {
  variant?: TypographyVariant;
  muted?: boolean;
  color?: string;
}

/** Big text grows less with the system font size, so totals and titles do not overflow small screens. */
const MAX_SCALE: Partial<Record<TypographyVariant, number>> = { display: 1.3, title: 1.5 };

export function AppText({ variant = 'body', muted = false, color, style, ...rest }: AppTextProps) {
  const { colors } = useTheme();
  return (
    <Text
      accessibilityRole={variant === 'title' ? 'header' : undefined}
      maxFontSizeMultiplier={MAX_SCALE[variant]}
      {...rest}
      style={[typography[variant], { color: color ?? (muted ? colors.textMuted : colors.text) }, style]}
    />
  );
}
