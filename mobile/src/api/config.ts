import Constants from 'expo-constants';
import { Platform } from 'react-native';

/** Port the API listens on in development (see src/RentApp.Api/Properties/launchSettings.json). */
export const DEV_API_PORT = 5080;

interface ResolveInputs {
  /** EXPO_PUBLIC_API_URL; always wins when set. */
  configuredUrl?: string;
  /** Expo dev server host, e.g. "192.168.1.20:8081" (Constants.expoConfig.hostUri). */
  devServerHostUri?: string;
  isDev: boolean;
  isWeb: boolean;
}

/**
 * Works out where the API lives. In development with Expo Go the phone already reaches the computer
 * running `expo start`, so the API is assumed to be on that same host at DEV_API_PORT.
 * Release builds must set EXPO_PUBLIC_API_URL; there is no fallback.
 */
export function resolveApiBaseUrl({ configuredUrl, devServerHostUri, isDev, isWeb }: ResolveInputs): string | null {
  const configured = configuredUrl?.trim();
  if (configured) {
    return configured.replace(/\/+$/, '');
  }

  if (!isDev) {
    return null;
  }

  if (isWeb) {
    return `http://localhost:${DEV_API_PORT}`;
  }

  const host = devServerHostUri?.split(':')[0];
  return host ? `http://${host}:${DEV_API_PORT}` : null;
}

export const apiBaseUrl = resolveApiBaseUrl({
  // Must be referenced literally so Expo can inline it at build time.
  configuredUrl: process.env.EXPO_PUBLIC_API_URL,
  devServerHostUri: Constants.expoConfig?.hostUri,
  isDev: __DEV__,
  isWeb: Platform.OS === 'web',
});
