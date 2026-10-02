import { type PropsWithChildren, useEffect, useState } from 'react';
import { Animated, Easing } from 'react-native';

interface FadeInProps {
  /** Milliseconds before the animation starts (stagger list items with index × 40). */
  delay?: number;
  /** Slide in from below (default) or from the right. */
  from?: 'below' | 'right';
}

/**
 * Entrance animation: fades in while sliding a few points into place. Built on React Native's
 * Animated API (native driver), so it needs no extra library and behaves the same in tests.
 */
export function FadeIn({ delay = 0, from = 'below', children }: PropsWithChildren<FadeInProps>) {
  const [progress] = useState(() => new Animated.Value(0));

  useEffect(() => {
    const animation = Animated.timing(progress, {
      toValue: 1,
      duration: 280,
      delay,
      easing: Easing.out(Easing.cubic),
      useNativeDriver: true,
    });
    animation.start();
    return () => animation.stop();
  }, [delay, progress]);

  const offset = progress.interpolate({ inputRange: [0, 1], outputRange: [14, 0] });
  const transform = from === 'right' ? [{ translateX: offset }] : [{ translateY: offset }];

  return <Animated.View style={{ opacity: progress, transform }}>{children}</Animated.View>;
}
