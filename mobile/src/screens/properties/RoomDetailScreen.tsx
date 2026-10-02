import { router } from 'expo-router';
import { Alert, RefreshControl, StyleSheet, View } from 'react-native';

import type { Bed, Room } from '@/api/properties';
import { useCurrentUser } from '@/auth/SessionProvider';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Card } from '@/components/Card';
import { ErrorState } from '@/components/ErrorState';
import { FormScreen } from '@/components/FormScreen';
import { InlineError } from '@/components/InlineError';
import { ListRow } from '@/components/ListRow';
import { LoadingState } from '@/components/LoadingState';
import { OCCUPANCY_TONE, OccupancyStats } from '@/components/OccupancyStats';
import { StatusPill } from '@/components/StatusPill';
import { useArchiveRoom, useRoom } from '@/hooks/useProperties';
import { radius } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';
import { formatRupees } from '@/utils/money';

export function RoomDetailScreen({ roomId }: { roomId: string }) {
  const query = useRoom(roomId);
  if (query.isPending) return <LoadingState message="Loading room…" />;
  if (query.error) {
    return (
      <FormScreen>
        <ErrorState error={query.error} action="load this room" onRetry={() => void query.refetch()} />
      </FormScreen>
    );
  }
  return <RoomDetail room={query.data} refetch={query.refetch} refreshing={query.isRefetching} />;
}

function RoomDetail({ room, refetch, refreshing }: { room: Room; refetch: () => unknown; refreshing: boolean }) {
  const { colors } = useTheme();
  const isOwner = useCurrentUser().role === 'Owner';
  const archive = useArchiveRoom(room.id);
  const archived = room.status === 'Archived';
  const full = room.beds.length >= room.capacity;

  const confirmArchive = () =>
    Alert.alert(`Archive room ${room.roomNumber}?`, 'The room and its beds will be removed from your lists. Nothing is deleted.', [
      { text: 'Cancel', style: 'cancel' },
      { text: 'Archive', style: 'destructive', onPress: () => archive.mutate(undefined, { onSuccess: () => router.back() }) },
    ]);

  return (
    <FormScreen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refetch()} />}>
      <Card>
        <View style={styles.titleRow}>
          <AppText variant="title" style={styles.flex}>
            Room {room.roomNumber}
          </AppText>
          {room.status !== 'Active' ? <StatusPill label={room.status} tone="neutral" /> : null}
        </View>
        <AppText muted>
          {room.beds.length} of {room.capacity} {room.capacity === 1 ? 'bed' : 'beds'}
          {room.roomType ? ` · ${room.roomType}` : ''}
        </AppText>
        <OccupancyStats summary={room.occupancy} />
      </Card>

      <View style={[styles.list, { borderColor: colors.border }]}>
        {room.beds.length === 0 ? (
          <AppText muted style={styles.empty}>
            No beds yet.
          </AppText>
        ) : (
          room.beds.map((bed) => <BedRow key={bed.id} bed={bed} editable={isOwner && !archived} />)
        )}
      </View>

      {isOwner && !archived ? (
        <>
          {full ? (
            <AppText variant="caption" muted>
              This room is full. Increase its capacity to add another bed.
            </AppText>
          ) : null}
          <Button
            label="Add bed"
            disabled={full}
            onPress={() => router.push({ pathname: '/rooms/[id]/beds/new', params: { id: room.id } })}
          />
          <Button label="Edit room" variant="secondary" onPress={() => router.push({ pathname: '/rooms/[id]/edit', params: { id: room.id } })} />
          <InlineError error={archive.error} action="archive the room" />
          <Button label="Archive room" variant="secondary" onPress={confirmArchive} loading={archive.isPending} />
        </>
      ) : null}
    </FormScreen>
  );
}

function BedRow({ bed, editable }: { bed: Bed; editable: boolean }) {
  const rent = bed.defaultMonthlyRent === null ? 'No rent set' : `${formatRupees(bed.defaultMonthlyRent)} / month`;
  // Spec layout: "A - Rahul - Occupied". Tenant names are only sent to users who may view tenants.
  const tenant = bed.tenant;
  const title = tenant ? `Bed ${bed.label} · ${tenant.fullName}` : `Bed ${bed.label}`;
  const onPress = tenant
    ? () => router.push({ pathname: '/tenants/[id]', params: { id: tenant.tenantId } })
    : editable
      ? () => router.push({ pathname: '/beds/[id]/edit', params: { id: bed.id } })
      : undefined;
  return (
    <ListRow
      title={title}
      subtitle={rent}
      trailing={<StatusPill label={bed.occupancy} tone={OCCUPANCY_TONE[bed.occupancy]} />}
      onPress={onPress}
      accessibilityHint={tenant ? 'Opens the tenant' : editable ? 'Edit this bed' : undefined}
    />
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  titleRow: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  list: { borderRadius: radius.lg, borderWidth: StyleSheet.hairlineWidth, overflow: 'hidden' },
  empty: { padding: 16 },
});
