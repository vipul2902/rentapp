import * as Haptics from 'expo-haptics';
import { type PropsWithChildren, useState } from 'react';
import { Animated, Platform, Pressable, type PressableProps, type StyleProp, type ViewStyle } from 'react-native';

interface PressableScaleProps extends Omit<PressableProps, 'style' | 'children'> {
  style?: StyleProp<ViewStyle>;
  /** Light haptic tap on press (default true). */
  haptic?: boolean;
}

/** A pressable that gently shrinks while held and gives a light haptic tap, so taps feel physical. */
export function PressableScale({ style, haptic = true, onPressIn, onPressOut, onPress, children, ...rest }: PropsWithChildren<PressableScaleProps>) {
  const [scale] = useState(() => new Animated.Value(1));
  const animate = (to: number) => Animated.spring(scale, { toValue: to, useNativeDriver: true, speed: 40, bounciness: 6 }).start();

  return (
    <Pressable
      {...rest}
      onPressIn={(e) => {
        animate(0.96);
        onPressIn?.(e);
      }}
      onPressOut={(e) => {
        animate(1);
        onPressOut?.(e);
      }}
      onPress={(e) => {
        if (haptic && Platform.OS !== 'web') void Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light).catch(() => undefined);
        onPress?.(e);
      }}
    >
      <Animated.View style={[style, { transform: [{ scale }] }]}>{children}</Animated.View>
    </Pressable>
  );
}
