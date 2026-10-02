import { router } from 'expo-router';
import { useState } from 'react';
import { FlatList, RefreshControl, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import type { Property } from '@/api/properties';
import { useCurrentUser } from '@/auth/SessionProvider';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { OccupancyStats } from '@/components/OccupancyStats';
import { FadeIn } from '@/components/FadeIn';
import { Icon } from '@/components/Icon';
import { PressableScale } from '@/components/PressableScale';
import { EmptyState, ProgressBar, SkeletonList } from '@/components/Visuals';
import { StatusPill } from '@/components/StatusPill';
import { TextField } from '@/components/TextField';
import { ToggleRow } from '@/components/ToggleRow';
import { usePropertyList } from '@/hooks/useProperties';
import { elevation, radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

export function PropertyListScreen() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const isOwner = useCurrentUser().role === 'Owner';
  const [draft, setDraft] = useState('');
  const [search, setSearch] = useState('');
  const [showArchived, setShowArchived] = useState(false);
  const query = usePropertyList(search, showArchived);

  const properties = query.data?.pages.flatMap((page) => page.items) ?? [];

  return (
    <FlatList
      style={{ backgroundColor: colors.background }}
      contentContainerStyle={[styles.content, { paddingBottom: insets.bottom + spacing.xl }]}
      data={properties}
      keyExtractor={(p) => p.id}
      renderItem={({ item, index }) => <PropertyCard property={item} index={index} />}
      onEndReached={() => {
        if (query.hasNextPage && !query.isFetchingNextPage) {
          void query.fetchNextPage();
        }
      }}
      onEndReachedThreshold={0.5}
      refreshControl={<RefreshControl refreshing={query.isRefetching} onRefresh={() => void query.refetch()} />}
      ListHeaderComponent={
        <View style={styles.header}>
          {isOwner ? <Button label="Add property" icon="add-circle-outline" onPress={() => router.push('/properties/new')} /> : null}
          <TextField
            label="Search"
            placeholder="Name or city"
            value={draft}
            onChangeText={setDraft}
            onSubmitEditing={() => setSearch(draft.trim())}
            returnKeyType="search"
          />
          {isOwner ? <ToggleRow label="Show archived" value={showArchived} onChange={setShowArchived} /> : null}
        </View>
      }
      ListEmptyComponent={
        query.isPending ? (
          <SkeletonList />
        ) : query.error ? (
          <ErrorState error={query.error} action="load properties" onRetry={() => void query.refetch()} retrying={query.isFetching} />
        ) : search ? (
          <EmptyState icon="search-outline" title="No match" message={`No property matches “${search}”.`} />
        ) : isOwner ? (
          <EmptyState icon="business-outline" title="Add your first PG" message="Add a property, then its rooms. Beds are created for you." actionLabel="Add property" onAction={() => router.push('/properties/new')} />
        ) : (
          <EmptyState icon="business-outline" title="No properties yet" />
        )
      }
      ListFooterComponent={query.isFetchingNextPage ? <LoadingState message="Loading more…" /> : null}
    />
  );
}

function PropertyCard({ property, index }: { property: Property; index: number }) {
  const { colors } = useTheme();
  const archived = property.status === 'Archived';
  const o = property.occupancy;
  return (
    <FadeIn delay={Math.min(index, 8) * 50}>
      <PressableScale
        accessibilityRole="button"
        accessibilityLabel={`${property.name}, ${property.city}, ${o.vacant} vacant of ${o.totalBeds} beds`}
        onPress={() => router.push({ pathname: '/properties/[id]', params: { id: property.id } })}
        style={[styles.card, { backgroundColor: colors.surface, opacity: archived ? 0.7 : 1 }, elevation(colors.shadow)]}
      >
        <View style={styles.cardHeader}>
          <View style={[styles.cardIcon, { backgroundColor: colors.primarySoft }]}>
            <Icon name="business" size={22} color={colors.primary} />
          </View>
          <View style={styles.cardTitle}>
            <AppText variant="heading">{property.name}</AppText>
            <AppText muted>
              {property.city} · {property.roomCount} {property.roomCount === 1 ? 'room' : 'rooms'}
            </AppText>
          </View>
          {archived ? <StatusPill label="Archived" tone="neutral" /> : null}
        </View>
        <ProgressBar value={o.occupied} total={o.totalBeds} />
        <OccupancyStats summary={o} />
      </PressableScale>
    </FadeIn>
  );
}

const styles = StyleSheet.create({
  content: { padding: spacing.lg, gap: spacing.md },
  header: { gap: spacing.md, marginBottom: spacing.sm },
  card: { borderRadius: radius.lg, padding: spacing.lg, gap: spacing.md },
  cardIcon: { width: 44, height: 44, borderRadius: 14, alignItems: 'center', justifyContent: 'center' },
  cardHeader: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  cardTitle: { flex: 1, gap: spacing.xs },
  empty: { paddingVertical: spacing.xl, textAlign: 'center' },
});
