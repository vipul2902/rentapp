import { apiRequest } from './client';

export type HealthStatus = 'Healthy' | 'Degraded' | 'Unhealthy';

export interface HealthCheck {
  name: string;
  status: HealthStatus;
  durationMs: number;
}

export interface HealthReport {
  status: HealthStatus;
  totalDurationMs: number;
  checks: HealthCheck[];
}

/** Readiness of the API and its dependencies. A 503 still carries a report, so it is not an error here. */
export function getReadiness(signal?: AbortSignal): Promise<HealthReport> {
  return apiRequest<HealthReport>('/health/ready', { signal, allowStatuses: [503], timeoutMs: 10_000 });
}
