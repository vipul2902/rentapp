import Ionicons from '@expo/vector-icons/Ionicons';
import type { ComponentProps } from 'react';
import type { ColorValue } from 'react-native';

export type IconName = ComponentProps<typeof Ionicons>['name'];

/** All app icons come from Ionicons (bundled with Expo). Decorative by default: hidden from screen readers. */
export function Icon({ name, size = 20, color, label }: { name: IconName; size?: number; color: ColorValue; label?: string }) {
  return (
    <Ionicons
      name={name}
      size={size}
      color={color}
      accessibilityLabel={label}
      accessibilityElementsHidden={!label}
      importantForAccessibility={label ? 'yes' : 'no-hide-descendants'}
    />
  );
}
