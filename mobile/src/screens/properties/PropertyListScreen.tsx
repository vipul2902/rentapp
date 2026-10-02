import { router } from 'expo-router';
import { useState } from 'react';
import { FlatList, Pressable, RefreshControl, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import type { Property } from '@/api/properties';
import { useCurrentUser } from '@/auth/SessionProvider';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { LoadingState } from '@/components/LoadingState';
import { OccupancyStats } from '@/components/OccupancyStats';
import { StatusPill } from '@/components/StatusPill';
import { TextField } from '@/components/TextField';
import { ToggleRow } from '@/components/ToggleRow';
import { usePropertyList } from '@/hooks/useProperties';
import { radius, spacing } from '@/theme/tokens';
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
      renderItem={({ item }) => <PropertyCard property={item} />}
      onEndReached={() => {
        if (query.hasNextPage && !query.isFetchingNextPage) {
          void query.fetchNextPage();
        }
      }}
      onEndReachedThreshold={0.5}
      refreshControl={<RefreshControl refreshing={query.isRefetching} onRefresh={() => void query.refetch()} />}
      ListHeaderComponent={
        <View style={styles.header}>
          {isOwner ? <Button label="Add property" onPress={() => router.push('/properties/new')} /> : null}
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
          <LoadingState message="Loading properties…" />
        ) : query.error ? (
          <ErrorState error={query.error} action="load properties" onRetry={() => void query.refetch()} retrying={query.isFetching} />
        ) : (
          <AppText muted style={styles.empty}>
            {search
              ? `No property matches “${search}”.`
              : isOwner
                ? 'No properties yet. Add your first PG to get started.'
                : 'No properties have been added yet.'}
          </AppText>
        )
      }
      ListFooterComponent={query.isFetchingNextPage ? <LoadingState message="Loading more…" /> : null}
    />
  );
}

function PropertyCard({ property }: { property: Property }) {
  const { colors } = useTheme();
  const archived = property.status === 'Archived';
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`${property.name}, ${property.city}, ${property.occupancy.vacant} vacant of ${property.occupancy.totalBeds} beds`}
      onPress={() => router.push({ pathname: '/properties/[id]', params: { id: property.id } })}
      style={({ pressed }) => [
        styles.card,
        { backgroundColor: pressed ? colors.surfaceMuted : colors.surface, borderColor: colors.border, opacity: archived ? 0.7 : 1 },
      ]}
    >
      <View style={styles.cardHeader}>
        <View style={styles.cardTitle}>
          <AppText variant="heading">{property.name}</AppText>
          <AppText muted>
            {property.city} · {property.roomCount} {property.roomCount === 1 ? 'room' : 'rooms'}
          </AppText>
        </View>
        {archived ? <StatusPill label="Archived" tone="neutral" /> : null}
      </View>
      <OccupancyStats summary={property.occupancy} />
    </Pressable>
  );
}

const styles = StyleSheet.create({
  content: { padding: spacing.lg, gap: spacing.md },
  header: { gap: spacing.md, marginBottom: spacing.sm },
  card: { borderRadius: radius.lg, borderWidth: StyleSheet.hairlineWidth, padding: spacing.lg, gap: spacing.md },
  cardHeader: { flexDirection: 'row', alignItems: 'flex-start', gap: spacing.md },
  cardTitle: { flex: 1, gap: spacing.xs },
  empty: { paddingVertical: spacing.xl, textAlign: 'center' },
});
