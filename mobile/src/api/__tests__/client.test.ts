import { apiRequest, CORRELATION_HEADER } from '../client';
import { ApiClientError } from '../errors';

const BASE = 'http://api.test';

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

describe('apiRequest', () => {
  const fetchMock = jest.fn<Promise<Response>, [string, RequestInit]>();

  beforeEach(() => {
    fetchMock.mockReset();
    globalThis.fetch = fetchMock as unknown as typeof fetch;
  });

  it('returns parsed JSON on success and sends a correlation id', async () => {
    fetchMock.mockResolvedValue(jsonResponse(200, { status: 'Healthy' }));

    const result = await apiRequest<{ status: string }>('/health/ready', { baseUrl: BASE });

    expect(result).toEqual({ status: 'Healthy' });
    const [url, init] = fetchMock.mock.calls[0]!;
    expect(url).toBe(`${BASE}/health/ready`);
    expect((init.headers as Record<string, string>)[CORRELATION_HEADER]).toMatch(/^m-[0-9a-f]+-[0-9a-f]+$/);
  });

  it('serializes JSON bodies', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }));

    await apiRequest('/payments', { baseUrl: BASE, method: 'POST', body: { amount: '8500.00' } });

    const [, init] = fetchMock.mock.calls[0]!;
    expect(init.method).toBe('POST');
    expect(init.body).toBe('{"amount":"8500.00"}');
    expect((init.headers as Record<string, string>)['Content-Type']).toBe('application/json');
  });

  it('maps API error bodies to ApiClientError', async () => {
    fetchMock.mockResolvedValue(
      jsonResponse(404, { code: 'RENT_CHARGE_NOT_FOUND', message: 'The requested rent charge was not found.', traceId: 't-1' }),
    );

    const error = await apiRequest('/rent/charges/1', { baseUrl: BASE }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiClientError);
    expect(error).toMatchObject({
      kind: 'http',
      status: 404,
      code: 'RENT_CHARGE_NOT_FOUND',
      serverMessage: 'The requested rent charge was not found.',
      traceId: 't-1',
    });
  });

  it('returns the body for explicitly allowed error statuses', async () => {
    fetchMock.mockResolvedValue(jsonResponse(503, { status: 'Unhealthy' }));

    await expect(apiRequest('/health/ready', { baseUrl: BASE, allowStatuses: [503] })).resolves.toEqual({ status: 'Unhealthy' });
  });

  it('reports network failures as kind "network"', async () => {
    fetchMock.mockRejectedValue(new TypeError('Network request failed'));

    await expect(apiRequest('/x', { baseUrl: BASE })).rejects.toMatchObject({ kind: 'network' });
  });

  it('reports timeouts as kind "timeout"', async () => {
    fetchMock.mockImplementation(
      (_url, init) =>
        new Promise((_resolve, reject) => {
          init.signal?.addEventListener('abort', () => reject(new Error('aborted')));
        }),
    );

    await expect(apiRequest('/slow', { baseUrl: BASE, timeoutMs: 10 })).rejects.toMatchObject({ kind: 'timeout' });
  });

  it('fails fast when no API address is configured', async () => {
    await expect(apiRequest('/x', { baseUrl: null })).rejects.toMatchObject({ kind: 'config' });
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
