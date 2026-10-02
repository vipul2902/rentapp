import { Stack } from 'expo-router';

export default function AuthLayout() {
  return (
    <Stack>
      <Stack.Screen name="sign-in" options={{ headerShown: false }} />
      <Stack.Screen name="register" options={{ title: 'Create account' }} />
      <Stack.Screen name="forgot-password" options={{ title: 'Forgot password' }} />
    </Stack>
  );
}
