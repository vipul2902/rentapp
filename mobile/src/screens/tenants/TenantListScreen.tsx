import { router } from 'expo-router';
import { useState } from 'react';
import { FlatList, RefreshControl, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import type { TenantFilter, TenantSummary } from '@/api/tenants';
import { useCurrentUser } from '@/auth/SessionProvider';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { Avatar } from '@/components/Avatar';
import { FadeIn } from '@/components/FadeIn';
import { PressableScale } from '@/components/PressableScale';
import { ChipBar, EmptyState, SkeletonList } from '@/components/Visuals';
import { StatusPill } from '@/components/StatusPill';
import { SearchField } from '@/components/SearchField';
import { useTenantList } from '@/hooks/useTenants';
import { elevation, radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';
import { formatRupees } from '@/utils/money';

const FILTERS = [
  { value: 'Current', label: 'Current', icon: 'home-outline' },
  { value: 'Overdue', label: 'Overdue', icon: 'alert-circle-outline' },
  { value: 'Former', label: 'Former', icon: 'exit-outline' },
  { value: 'Unassigned', label: 'No bed', icon: 'bed-outline' },
  { value: 'All', label: 'All', icon: 'list-outline' },
] as const;

export function TenantListScreen() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const isOwner = useCurrentUser().role === 'Owner';
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
      renderItem={({ item, index }) => <TenantCard tenant={item} index={index} />}
      ItemSeparatorComponent={() => <View style={styles.gap} />}
      onEndReached={() => {
        if (query.hasNextPage && !query.isFetchingNextPage) void query.fetchNextPage();
      }}
      onEndReachedThreshold={0.5}
      refreshControl={<RefreshControl refreshing={query.isRefetching} onRefresh={() => void query.refetch()} />}
      ListHeaderComponent={
        <View style={styles.header}>
          {isOwner ? <Button label="Add tenant" icon="person-add-outline" onPress={() => router.push('/tenants/new')} /> : null}
          <SearchField placeholder="Name, phone or room number" onSearch={setSearch} />
          <ChipBar label="Show" options={FILTERS} value={filter} onChange={setFilter} />
        </View>
      }
      ListEmptyComponent={
        query.isPending ? (
          <SkeletonList />
        ) : query.error ? (
          <ErrorState error={query.error} action="load tenants" onRetry={() => void query.refetch()} retrying={query.isFetching} />
        ) : search ? (
          <EmptyState icon="search-outline" title="No match" message={`No tenant matches “${search}”.`} />
        ) : filter === 'Overdue' ? (
          <EmptyState icon="happy-outline" title="Nobody is overdue" message="Every tenant is up to date." />
        ) : isOwner && filter === 'Current' ? (
          <EmptyState icon="people-outline" title="No tenants yet" message="Add your first tenant and give them a bed." actionLabel="Add tenant" onAction={() => router.push('/tenants/new')} />
        ) : (
          <EmptyState icon="people-outline" title="Nobody here" />
        )
      }
      ListFooterComponent={query.isFetchingNextPage ? <LoadingState message="Loading more…" /> : null}
    />
  );
}

function TenantCard({ tenant, index }: { tenant: TenantSummary; index: number }) {
  const { colors } = useTheme();
  const tenancy = tenant.currentTenancy;
  const place = tenancy ? `${tenancy.propertyName} · Room ${tenancy.roomNumber} · Bed ${tenancy.bedLabel}` : 'No bed';
  return (
    <FadeIn delay={Math.min(index, 8) * 40}>
      <PressableScale
        accessibilityRole="button"
        accessibilityLabel={`${tenant.fullName}, ${place}`}
        onPress={() => router.push({ pathname: '/tenants/[id]', params: { id: tenant.id } })}
        style={[styles.card, { backgroundColor: colors.surface }, elevation(colors.shadow)]}
      >
        <Avatar name={tenant.fullName} />
        <View style={styles.flex}>
          <View style={styles.row}>
            <AppText variant="heading" style={styles.flex} numberOfLines={1}>
              {tenant.fullName}
            </AppText>
            {tenancy?.state === 'Upcoming' ? <StatusPill label="Moving in" tone="warning" icon="time" /> : null}
          </View>
          <AppText variant="caption" muted numberOfLines={1}>
            {place}
          </AppText>
          <View style={styles.row}>
            <AppText variant="caption" muted style={styles.flex}>
              {tenant.phone}
            </AppText>
            {tenancy ? <AppText variant="label">{formatRupees(tenancy.monthlyRent)} / month</AppText> : null}
          </View>
          {tenant.overdueAmount > 0 ? (
            <StatusPill label={`${formatRupees(tenant.overdueAmount)} overdue`} tone="danger" icon="alert-circle" />
          ) : null}
        </View>
      </PressableScale>
    </FadeIn>
  );
}

const styles = StyleSheet.create({
  content: { padding: spacing.lg, gap: spacing.md },
  header: { gap: spacing.md, marginBottom: spacing.sm },
  card: { flexDirection: 'row', alignItems: 'flex-start', gap: spacing.md, borderRadius: radius.lg, padding: spacing.lg },
  gap: { height: spacing.md },
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  flex: { flex: 1 },
  empty: { paddingVertical: spacing.xl, textAlign: 'center' },
});
