import { router } from 'expo-router';
import { Alert, Pressable, RefreshControl, StyleSheet, View } from 'react-native';

import type { Property, Room } from '@/api/properties';
import { useCurrentUser } from '@/auth/SessionProvider';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Card } from '@/components/Card';
import { ErrorState } from '@/components/ErrorState';
import { FormScreen } from '@/components/FormScreen';
import { InlineError } from '@/components/InlineError';
import { LoadingState } from '@/components/LoadingState';
import { OCCUPANCY_TONE, OccupancyStats } from '@/components/OccupancyStats';
import { StatusPill } from '@/components/StatusPill';
import { useArchiveProperty, useProperty, useRestoreProperty, useRooms } from '@/hooks/useProperties';
import { radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

export function PropertyDetailScreen({ propertyId }: { propertyId: string }) {
  const property = useProperty(propertyId);
  const rooms = useRooms(propertyId);

  if (property.isPending) return <LoadingState message="Loading property…" />;
  if (property.error) {
    return (
      <FormScreen>
        <ErrorState error={property.error} action="load this property" onRetry={() => void property.refetch()} />
      </FormScreen>
    );
  }

  const refreshing = property.isRefetching || rooms.isRefetching;
  const refresh = () => {
    void property.refetch();
    void rooms.refetch();
  };

  return (
    <FormScreen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={refresh} />}>
      <Overview property={property.data} />

      <View style={styles.sectionHeader}>
        <AppText variant="heading">Rooms</AppText>
        <AppText muted>{property.data.roomCount}</AppText>
      </View>
      {rooms.isPending ? (
        <LoadingState message="Loading rooms…" />
      ) : rooms.error ? (
        <ErrorState error={rooms.error} action="load rooms" onRetry={() => void rooms.refetch()} />
      ) : (
        <RoomList rooms={rooms.data} propertyId={propertyId} archived={property.data.status === 'Archived'} />
      )}

      <AppText variant="caption" muted>
        Tenants, rent and payments for this property arrive in the next phases.
      </AppText>
    </FormScreen>
  );
}

function Overview({ property }: { property: Property }) {
  const isOwner = useCurrentUser().role === 'Owner';
  const archive = useArchiveProperty(property.id);
  const restore = useRestoreProperty(property.id);
  const archived = property.status === 'Archived';

  const confirmArchive = () =>
    Alert.alert(`Archive ${property.name}?`, 'It will be hidden from your lists. Nothing is deleted, and you can restore it later.', [
      { text: 'Cancel', style: 'cancel' },
      { text: 'Archive', style: 'destructive', onPress: () => archive.mutate(undefined, { onSuccess: () => router.back() }) },
    ]);

  const address = [property.address, property.city, property.state, property.postalCode].filter(Boolean).join(', ');

  return (
    <Card>
      <View style={styles.titleRow}>
        <AppText variant="title" style={styles.flex}>
          {property.name}
        </AppText>
        {archived ? <StatusPill label="Archived" tone="neutral" /> : null}
      </View>
      <AppText muted selectable>
        {address}
      </AppText>
      {property.contactPhone ? (
        <AppText muted selectable>
          {property.contactPhone}
        </AppText>
      ) : null}
      <OccupancyStats summary={property.occupancy} />

      {isOwner ? (
        <View style={styles.actions}>
          <InlineError error={archive.error ?? restore.error} action={archived ? 'restore the property' : 'archive the property'} />
          {archived ? (
            <Button label="Restore property" onPress={() => restore.mutate(undefined)} loading={restore.isPending} />
          ) : (
            <>
              <Button
                label="Edit details"
                variant="secondary"
                onPress={() => router.push({ pathname: '/properties/[id]/edit', params: { id: property.id } })}
              />
              <Button label="Archive property" variant="secondary" onPress={confirmArchive} loading={archive.isPending} />
            </>
          )}
        </View>
      ) : null}
    </Card>
  );
}

function RoomList({ rooms, propertyId, archived }: { rooms: Room[]; propertyId: string; archived: boolean }) {
  const isOwner = useCurrentUser().role === 'Owner';
  return (
    <View style={styles.rooms}>
      {rooms.length === 0 ? (
        <AppText muted>{isOwner ? 'No rooms yet. Add a room and its beds are created for you.' : 'No rooms yet.'}</AppText>
      ) : (
        rooms.map((room) => <RoomCard key={room.id} room={room} />)
      )}
      {isOwner && !archived ? (
        <Button label="Add room" onPress={() => router.push({ pathname: '/properties/[id]/rooms/new', params: { id: propertyId } })} />
      ) : null}
    </View>
  );
}

function RoomCard({ room }: { room: Room }) {
  const { colors } = useTheme();
  const unavailable = room.status === 'Unavailable';
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`Room ${room.roomNumber}, ${room.beds.length} beds, ${room.occupancy.vacant} vacant`}
      onPress={() => router.push({ pathname: '/rooms/[id]', params: { id: room.id } })}
      style={({ pressed }) => [styles.roomCard, { backgroundColor: pressed ? colors.surfaceMuted : colors.surface, borderColor: colors.border }]}
    >
      <View style={styles.titleRow}>
        <View style={styles.flex}>
          <AppText variant="heading">Room {room.roomNumber}</AppText>
          <AppText muted>
            {room.beds.length} {room.beds.length === 1 ? 'bed' : 'beds'}
            {room.roomType ? ` · ${room.roomType}` : ''}
          </AppText>
        </View>
        {unavailable ? <StatusPill label="Unavailable" tone="neutral" /> : null}
      </View>
      {room.beds.map((bed) => (
        <View key={bed.id} style={styles.bedRow}>
          <AppText style={styles.flex}>Bed {bed.label}</AppText>
          <StatusPill label={bed.occupancy} tone={OCCUPANCY_TONE[bed.occupancy]} />
        </View>
      ))}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  titleRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  actions: { gap: spacing.sm },
  sectionHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'baseline' },
  rooms: { gap: spacing.md },
  roomCard: { borderRadius: radius.lg, borderWidth: StyleSheet.hairlineWidth, padding: spacing.lg, gap: spacing.sm },
  bedRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
});
