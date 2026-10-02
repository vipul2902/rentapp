import { ApiClientError } from '../errors';
import { shouldRetry } from '../queryClient';

describe('shouldRetry', () => {
  it('retries transient failures a limited number of times', () => {
    const network = new ApiClientError({ kind: 'network' });
    expect(shouldRetry(0, network)).toBe(true);
    expect(shouldRetry(1, network)).toBe(true);
    expect(shouldRetry(2, network)).toBe(false);
    expect(shouldRetry(0, new ApiClientError({ kind: 'http', status: 503 }))).toBe(true);
  });

  it('does not retry client errors', () => {
    expect(shouldRetry(0, new ApiClientError({ kind: 'http', status: 400 }))).toBe(false);
    expect(shouldRetry(0, new ApiClientError({ kind: 'http', status: 404 }))).toBe(false);
    expect(shouldRetry(0, new Error('unknown'))).toBe(false);
  });
});
