import { router } from 'expo-router';
import { useState } from 'react';
import { FlatList, RefreshControl, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import type { StaffMember } from '@/api/types';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { ErrorState } from '@/components/ErrorState';
import { ListRow } from '@/components/ListRow';
import { LoadingState } from '@/components/LoadingState';
import { StatusPill } from '@/components/StatusPill';
import { TextField } from '@/components/TextField';
import { useStaffList } from '@/hooks/useStaff';
import { spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

export function StaffListScreen() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const [draft, setDraft] = useState('');
  const [search, setSearch] = useState('');
  const query = useStaffList(search);

  const members = query.data?.pages.flatMap((page) => page.items) ?? [];

  return (
    <FlatList
      style={{ backgroundColor: colors.background }}
      contentContainerStyle={[styles.content, { paddingBottom: insets.bottom + spacing.xl }]}
      data={members}
      keyExtractor={(member) => member.id}
      ItemSeparatorComponent={() => <View style={[styles.separator, { backgroundColor: colors.border }]} />}
      renderItem={({ item }) => <StaffRow member={item} />}
      onEndReached={() => {
        if (query.hasNextPage && !query.isFetchingNextPage) {
          void query.fetchNextPage();
        }
      }}
      onEndReachedThreshold={0.5}
      refreshControl={<RefreshControl refreshing={query.isRefetching} onRefresh={() => void query.refetch()} />}
      ListHeaderComponent={
        <View style={styles.header}>
          <Button label="Add staff" onPress={() => router.push('/staff/new')} />
          <TextField
            label="Search"
            placeholder="Name or email"
            value={draft}
            onChangeText={setDraft}
            onSubmitEditing={() => setSearch(draft.trim())}
            returnKeyType="search"
            autoCapitalize="none"
          />
        </View>
      }
      ListEmptyComponent={
        query.isPending ? (
          <LoadingState message="Loading staff…" />
        ) : query.error ? (
          <ErrorState error={query.error} action="load staff" onRetry={() => void query.refetch()} retrying={query.isFetching} />
        ) : (
          <AppText muted style={styles.empty}>
            {search ? `No one matches “${search}”.` : 'No staff yet. Add someone to help collect rent.'}
          </AppText>
        )
      }
      ListFooterComponent={query.isFetchingNextPage ? <LoadingState message="Loading more…" /> : null}
    />
  );
}

function StaffRow({ member }: { member: StaffMember }) {
  const isOwner = member.role === 'Owner';
  const subtitle = isOwner ? `${member.email} · Owner` : `${member.email} · ${member.permissions.length} of 5 permissions`;
  return (
    <ListRow
      title={member.name}
      subtitle={subtitle}
      trailing={member.status === 'Disabled' ? <StatusPill label="Disabled" tone="neutral" /> : null}
      onPress={isOwner ? undefined : () => router.push({ pathname: '/staff/[id]', params: { id: member.id } })}
      accessibilityHint={isOwner ? undefined : 'Opens permissions and account options'}
    />
  );
}

const styles = StyleSheet.create({
  content: { padding: spacing.lg },
  header: { gap: spacing.md, marginBottom: spacing.lg },
  separator: { height: StyleSheet.hairlineWidth },
  empty: { paddingVertical: spacing.xl, textAlign: 'center' },
});
