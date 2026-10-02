import { useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { FlatList, RefreshControl, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import type { RentFilter } from '@/api/rent';
import { useCurrentUser } from '@/auth/SessionProvider';
import { AppText } from '@/components/AppText';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { RentChargeCard } from '@/components/RentChargeCard';
import { ChipBar, EmptyState, SkeletonList } from '@/components/Visuals';
import { useCharges, useGenerateCharges, useRentSummary } from '@/hooks/useRent';
import { spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';
import { formatRupees } from '@/utils/money';

const EMPTY: Record<RentFilter, { title: string; message: string }> = {
  Outstanding: { title: 'All caught up', message: 'Nobody owes rent right now.' },
  Overdue: { title: 'Nobody is overdue', message: 'Every due date so far has been met.' },
  DueToday: { title: 'Nothing due today', message: 'Enjoy the quiet day.' },
  Upcoming: { title: 'Nothing coming up', message: 'Dues appear here a week before they are due.' },
  Paid: { title: 'No settled rent yet', message: 'Paid and waived dues show up here.' },
  All: { title: 'No rent dues yet', message: 'Add a tenant to a bed and their monthly rent appears here.' },
};

const FILTERS: RentFilter[] = ['Outstanding', 'Overdue', 'DueToday', 'Upcoming', 'Paid', 'All'];

export function RentScreen() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const isOwner = useCurrentUser().role === 'Owner';
  const params = useLocalSearchParams<{ filter?: string }>();
  const initial = FILTERS.includes(params.filter as RentFilter) ? (params.filter as RentFilter) : 'Outstanding';
  const [filter, setFilter] = useState<RentFilter>(initial);

  const summary = useRentSummary();
  const query = useCharges(filter);
  const generate = useGenerateCharges();
  const charges = query.data?.pages.flatMap((p) => p.items) ?? [];

  const refresh = () => (isOwner ? generate.mutate() : void query.refetch());
  const s = summary.data;

  return (
    <FlatList
      style={{ backgroundColor: colors.background }}
      contentContainerStyle={[styles.content, { paddingBottom: insets.bottom + spacing.xl }]}
      data={charges}
      keyExtractor={(c) => c.id}
      renderItem={({ item, index }) => <RentChargeCard charge={item} index={index} />}
      onEndReached={() => {
        if (query.hasNextPage && !query.isFetchingNextPage) void query.fetchNextPage();
      }}
      onEndReachedThreshold={0.5}
      refreshControl={<RefreshControl refreshing={query.isRefetching || generate.isPending} onRefresh={refresh} tintColor={colors.primary} />}
      ListHeaderComponent={
        <View style={styles.header}>
          {s ? (
            <View accessible accessibilityLabel={`${formatRupees(s.outstanding.amount)} to collect, ${formatRupees(s.overdue.amount)} overdue`}>
              <AppText variant="display">{formatRupees(s.outstanding.amount)}</AppText>
              <AppText muted>
                to collect · <AppText color={colors.danger}>{formatRupees(s.overdue.amount)} overdue</AppText>
              </AppText>
            </View>
          ) : null}
          <ChipBar
            label="Show"
            value={filter}
            onChange={setFilter}
            options={[
              { value: 'Outstanding', label: 'To collect', icon: 'wallet-outline', count: s?.outstanding.count },
              { value: 'Overdue', label: 'Overdue', icon: 'alert-circle-outline', count: s?.overdue.count },
              { value: 'DueToday', label: 'Due today', icon: 'today-outline', count: s?.dueToday.count },
              { value: 'Upcoming', label: 'Upcoming', icon: 'time-outline' },
              { value: 'Paid', label: 'Paid', icon: 'checkmark-circle-outline' },
              { value: 'All', label: 'All', icon: 'list-outline' },
            ]}
          />
        </View>
      }
      ItemSeparatorComponent={() => <View style={styles.gap} />}
      ListEmptyComponent={
        query.isPending ? (
          <SkeletonList />
        ) : query.error ? (
          <ErrorState error={query.error} action="load rent dues" onRetry={() => void query.refetch()} retrying={query.isFetching} />
        ) : (
          <EmptyState icon={filter === 'Overdue' || filter === 'Outstanding' ? 'happy-outline' : 'calendar-outline'} {...EMPTY[filter]} />
        )
      }
      ListFooterComponent={query.isFetchingNextPage ? <LoadingState message="Loading more…" /> : null}
    />
  );
}

const styles = StyleSheet.create({
  content: { padding: spacing.lg },
  header: { gap: spacing.lg, marginBottom: spacing.lg },
  gap: { height: spacing.md },
});
