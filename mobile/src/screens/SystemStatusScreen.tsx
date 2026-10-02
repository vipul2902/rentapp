import { RefreshControl, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { apiBaseUrl } from '@/api/config';
import type { HealthStatus } from '@/api/health';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Card } from '@/components/Card';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { StatusPill, type StatusTone } from '@/components/StatusPill';
import { useReadiness } from '@/hooks/useReadiness';
import { spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

const CHECK_LABELS: Record<string, string> = {
  postgres: 'Database (PostgreSQL)',
  redis: 'Cache (Redis)',
};

const TONES: Record<HealthStatus, StatusTone> = {
  Healthy: 'success',
  Degraded: 'warning',
  Unhealthy: 'danger',
};

/**
 * Phase 1 developer screen: proves the phone can reach the API and the API can reach its dependencies.
 * It will move behind a settings/diagnostics entry once the real app screens exist.
 */
export function SystemStatusScreen() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const { data, error, isPending, isFetching, refetch, dataUpdatedAt } = useReadiness();

  return (
    <ScrollView
      style={{ backgroundColor: colors.background }}
      contentContainerStyle={[styles.content, { paddingBottom: insets.bottom + spacing.xl }]}
      refreshControl={<RefreshControl refreshing={isFetching && !isPending} onRefresh={() => void refetch()} />}
    >
      <Card>
        <AppText variant="heading">API server</AppText>
        <AppText muted selectable>
          {apiBaseUrl ?? 'Not configured'}
        </AppText>

        {isPending ? (
          <LoadingState message="Checking connection…" />
        ) : error ? (
          <ErrorState
            error={error}
            action="reach the server"
            onRetry={() => void refetch()}
            retrying={isFetching}
            hint="Make sure the API is running and your phone is on the same Wi-Fi as your computer."
          />
        ) : (
          <View style={styles.rows}>
            <StatusRow label="API" status="Healthy" />
            {data.checks.map((check) => (
              <StatusRow key={check.name} label={CHECK_LABELS[check.name] ?? check.name} status={check.status} />
            ))}
            <AppText variant="caption" muted>
              Last checked {new Date(dataUpdatedAt).toLocaleTimeString()}
            </AppText>
          </View>
        )}
      </Card>

      {!isPending && !error ? (
        <Button label="Check again" variant="secondary" onPress={() => void refetch()} loading={isFetching} />
      ) : null}
    </ScrollView>
  );
}

function StatusRow({ label, status }: { label: string; status: HealthStatus }) {
  return (
    <View style={styles.row} accessible accessibilityLabel={`${label}: ${status}`}>
      <AppText style={styles.rowLabel}>{label}</AppText>
      <StatusPill label={status} tone={TONES[status]} />
    </View>
  );
}

const styles = StyleSheet.create({
  content: { padding: spacing.lg, gap: spacing.lg },
  rows: { gap: spacing.md },
  row: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: spacing.md },
  rowLabel: { flexShrink: 1 },
});
