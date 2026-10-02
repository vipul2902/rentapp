/** In-memory stand-in for the native Keychain/Keystore, used automatically by Jest. */
const store = new Map<string, string>();

export const WHEN_UNLOCKED_THIS_DEVICE_ONLY = 'WHEN_UNLOCKED_THIS_DEVICE_ONLY';

export const getItemAsync = jest.fn(async (key: string) => store.get(key) ?? null);
export const setItemAsync = jest.fn(async (key: string, value: string) => {
  store.set(key, value);
});
export const deleteItemAsync = jest.fn(async (key: string) => {
  store.delete(key);
});

export function __reset(): void {
  store.clear();
  getItemAsync.mockClear();
  setItemAsync.mockClear();
  deleteItemAsync.mockClear();
}
