import { router } from 'expo-router';
import { useState } from 'react';
import { FlatList, Pressable, RefreshControl, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import type { TenantFilter, TenantSummary } from '@/api/tenants';
import { useCurrentUser } from '@/auth/SessionProvider';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { SegmentedControl } from '@/components/SegmentedControl';
import { StatusPill } from '@/components/StatusPill';
import { TextField } from '@/components/TextField';
import { useTenantList } from '@/hooks/useTenants';
import { radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';
import { formatRupees } from '@/utils/money';

const FILTERS = [
  { value: 'Current', label: 'Current' },
  { value: 'Former', label: 'Former' },
  { value: 'Unassigned', label: 'No bed' },
  { value: 'All', label: 'All' },
] as const;

export function TenantListScreen() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const isOwner = useCurrentUser().role === 'Owner';
  const [draft, setDraft] = useState('');
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState<TenantFilter>('Current');
  const query = useTenantList(search, filter);
  const tenants = query.data?.pages.flatMap((p) => p.items) ?? [];

  return (
    <FlatList
      style={{ backgroundColor: colors.background }}
      contentContainerStyle={[styles.content, { paddingBottom: insets.bottom + spacing.xl }]}
      data={tenants}
      keyExtractor={(t) => t.id}
      renderItem={({ item }) => <TenantCard tenant={item} />}
      onEndReached={() => {
        if (query.hasNextPage && !query.isFetchingNextPage) void query.fetchNextPage();
      }}
      onEndReachedThreshold={0.5}
      refreshControl={<RefreshControl refreshing={query.isRefetching} onRefresh={() => void query.refetch()} />}
      ListHeaderComponent={
        <View style={styles.header}>
          {isOwner ? <Button label="Add tenant" onPress={() => router.push('/tenants/new')} /> : null}
          <TextField
            label="Search"
            placeholder="Name, phone or room number"
            value={draft}
            onChangeText={setDraft}
            onSubmitEditing={() => setSearch(draft.trim())}
            returnKeyType="search"
          />
          <SegmentedControl label="Show" options={FILTERS} value={filter} onChange={setFilter} />
        </View>
      }
      ListEmptyComponent={
        query.isPending ? (
          <LoadingState message="Loading tenants…" />
        ) : query.error ? (
          <ErrorState error={query.error} action="load tenants" onRetry={() => void query.refetch()} retrying={query.isFetching} />
        ) : (
          <AppText muted style={styles.empty}>
            {search ? `No tenant matches “${search}”.` : isOwner && filter === 'Current' ? 'No tenants yet. Add your first tenant.' : 'Nobody here.'}
          </AppText>
        )
      }
      ListFooterComponent={query.isFetchingNextPage ? <LoadingState message="Loading more…" /> : null}
    />
  );
}

function TenantCard({ tenant }: { tenant: TenantSummary }) {
  const { colors } = useTheme();
  const tenancy = tenant.currentTenancy;
  const place = tenancy ? `${tenancy.propertyName} · Room ${tenancy.roomNumber} · Bed ${tenancy.bedLabel}` : 'No bed';
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`${tenant.fullName}, ${place}`}
      onPress={() => router.push({ pathname: '/tenants/[id]', params: { id: tenant.id } })}
      style={({ pressed }) => [styles.card, { backgroundColor: pressed ? colors.surfaceMuted : colors.surface, borderColor: colors.border }]}
    >
      <View style={styles.row}>
        <AppText variant="heading" style={styles.flex}>
          {tenant.fullName}
        </AppText>
        {tenancy?.state === 'Upcoming' ? <StatusPill label="Moving in" tone="warning" /> : null}
      </View>
      <AppText muted>{place}</AppText>
      <View style={styles.row}>
        <AppText muted style={styles.flex}>
          {tenant.phone}
        </AppText>
        {tenancy ? <AppText variant="label">{formatRupees(tenancy.monthlyRent)} / month</AppText> : null}
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  content: { padding: spacing.lg, gap: spacing.md },
  header: { gap: spacing.md, marginBottom: spacing.sm },
  card: { borderRadius: radius.lg, borderWidth: StyleSheet.hairlineWidth, padding: spacing.lg, gap: spacing.xs },
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  flex: { flex: 1 },
  empty: { paddingVertical: spacing.xl, textAlign: 'center' },
});
