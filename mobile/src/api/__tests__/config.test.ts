import { DEV_API_PORT, resolveApiBaseUrl } from '../config';

describe('resolveApiBaseUrl', () => {
  it('prefers the configured URL and trims trailing slashes', () => {
    expect(
      resolveApiBaseUrl({ configuredUrl: ' https://api.example.com/ ', devServerHostUri: '10.0.0.2:8081', isDev: true, isWeb: false }),
    ).toBe('https://api.example.com');
  });

  it('uses the Expo dev server host in development', () => {
    expect(resolveApiBaseUrl({ devServerHostUri: '192.168.68.114:8081', isDev: true, isWeb: false })).toBe(
      `http://192.168.68.114:${DEV_API_PORT}`,
    );
  });

  it('uses localhost for the web preview', () => {
    expect(resolveApiBaseUrl({ isDev: true, isWeb: true })).toBe(`http://localhost:${DEV_API_PORT}`);
  });

  it('has no fallback in release builds', () => {
    expect(resolveApiBaseUrl({ devServerHostUri: '192.168.68.114:8081', isDev: false, isWeb: false })).toBeNull();
  });

  it('returns null when nothing is known', () => {
    expect(resolveApiBaseUrl({ isDev: true, isWeb: false })).toBeNull();
  });
});
