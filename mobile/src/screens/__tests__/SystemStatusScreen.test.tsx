import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react-native';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import { ApiClientError } from '@/api/errors';
import { getReadiness, type HealthReport } from '@/api/health';

import { SystemStatusScreen } from '../SystemStatusScreen';

jest.mock('@/api/health', () => ({ getReadiness: jest.fn() }));
const getReadinessMock = jest.mocked(getReadiness);

const TEST_METRICS = {
  frame: { x: 0, y: 0, width: 390, height: 844 },
  insets: { top: 0, left: 0, right: 0, bottom: 0 },
};

// First render pays the cold module-transform cost, so allow more than the 1s default.
const FIND = { timeout: 5_000 };
let client: QueryClient | undefined;

afterEach(() => client?.clear());

async function renderScreen() {
  // gcTime: Infinity avoids garbage-collection timers that would keep Jest alive after the run.
  client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: Infinity } } });
  await render(
    <SafeAreaProvider initialMetrics={TEST_METRICS}>
      <QueryClientProvider client={client}>
        <SystemStatusScreen />
      </QueryClientProvider>
    </SafeAreaProvider>,
  );
}

describe('SystemStatusScreen', () => {
  it('shows each dependency with its status', async () => {
    const report: HealthReport = {
      status: 'Unhealthy',
      totalDurationMs: 12,
      checks: [
        { name: 'postgres', status: 'Healthy', durationMs: 5 },
        { name: 'redis', status: 'Unhealthy', durationMs: 7 },
      ],
    };
    getReadinessMock.mockResolvedValue(report);

    await renderScreen();

    expect(await screen.findByLabelText('Database (PostgreSQL): Healthy', {}, FIND)).toBeTruthy();
    expect(screen.getByLabelText('Cache (Redis): Unhealthy')).toBeTruthy();
    expect(screen.getByLabelText('API: Healthy')).toBeTruthy();
  });

  it('shows a human-readable message when the API cannot be reached', async () => {
    getReadinessMock.mockRejectedValue(new ApiClientError({ kind: 'network' }));

    await renderScreen();

    expect(await screen.findByText('Unable to connect.', {}, FIND)).toBeTruthy();
    expect(screen.getByText('Please check your internet connection.')).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Try again' })).toBeTruthy();
  });
});
