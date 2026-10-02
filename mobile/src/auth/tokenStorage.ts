import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

const REFRESH_TOKEN_KEY = 'rentapp.refreshToken';

// Keychain/Keystore-backed, not synced to other devices or backups.
const OPTIONS: SecureStore.SecureStoreOptions = {
  keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
};

// SecureStore has no web implementation; the web preview keeps the session in memory only.
let webMemory: string | null = null;
const isWeb = Platform.OS === 'web';

/**
 * Only the refresh token is persisted. The access token lives in memory and is re-issued on start,
 * so nothing long-lived ever sits in plain AsyncStorage.
 */
export const tokenStorage = {
  load: (): Promise<string | null> => (isWeb ? Promise.resolve(webMemory) : SecureStore.getItemAsync(REFRESH_TOKEN_KEY, OPTIONS)),

  save: async (refreshToken: string): Promise<void> => {
    if (isWeb) {
      webMemory = refreshToken;
      return;
    }
    await SecureStore.setItemAsync(REFRESH_TOKEN_KEY, refreshToken, OPTIONS);
  },

  clear: async (): Promise<void> => {
    if (isWeb) {
      webMemory = null;
      return;
    }
    await SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY, OPTIONS);
  },
};
