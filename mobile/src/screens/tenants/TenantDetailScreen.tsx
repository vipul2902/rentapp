import { router } from 'expo-router';
import { Alert, Linking, Pressable, RefreshControl, StyleSheet, View } from 'react-native';

import type { Tenancy, TenantDetail } from '@/api/tenants';
import { useCurrentUser } from '@/auth/SessionProvider';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Card } from '@/components/Card';
import { ErrorState } from '@/components/ErrorState';
import { FormScreen } from '@/components/FormScreen';
import { InlineError } from '@/components/InlineError';
import { LoadingState } from '@/components/LoadingState';
import { StatusPill } from '@/components/StatusPill';
import { useArchiveTenant, useTenant } from '@/hooks/useTenants';
import { useCharges } from '@/hooks/useRent';
import { RentChargeCard } from '@/components/RentChargeCard';
import { Avatar } from '@/components/Avatar';
import { SectionHeader } from '@/components/Visuals';
import { spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';
import { formatDate, ordinal } from '@/utils/dates';
import { formatRupees } from '@/utils/money';

export function TenantDetailScreen({ tenantId }: { tenantId: string }) {
  const query = useTenant(tenantId);
  if (query.isPending) return <LoadingState message="Loading tenant…" />;
  if (query.error) {
    return (
      <FormScreen>
        <ErrorState error={query.error} action="load this tenant" onRetry={() => void query.refetch()} />
      </FormScreen>
    );
  }
  return <TenantDetailView tenant={query.data} refetch={query.refetch} refreshing={query.isRefetching} />;
}

function TenantDetailView({ tenant, refetch, refreshing }: { tenant: TenantDetail; refetch: () => unknown; refreshing: boolean }) {
  const isOwner = useCurrentUser().role === 'Owner';
  const archive = useArchiveTenant(tenant.id);
  const tenancy = tenant.currentTenancy;
  const past = tenant.history.filter((h) => h.status === 'Ended');
  const go = (pathname: '/tenants/[id]/edit' | '/tenants/[id]/move-in' | '/tenants/[id]/move-out' | '/tenants/[id]/move' | '/tenants/[id]/terms') =>
    router.push({ pathname, params: { id: tenant.id } });

  const confirmArchive = () =>
    Alert.alert(`Archive ${tenant.fullName}?`, 'They will be hidden from your tenant list. Their history is kept.', [
      { text: 'Cancel', style: 'cancel' },
      { text: 'Archive', style: 'destructive', onPress: () => archive.mutate(undefined, { onSuccess: () => router.back() }) },
    ]);

  return (
    <FormScreen refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void refetch()} />}>
      <Card>
        <View style={styles.row}>
          <Avatar name={tenant.fullName} size={56} />
          <View style={styles.flex}>
            <AppText variant="title">{tenant.fullName}</AppText>
            <PhoneLink phone={tenant.phone} />
          </View>
        </View>
        {tenant.email ? <AppText muted selectable>{tenant.email}</AppText> : null}
        {tenant.emergencyContactName || tenant.emergencyContactPhone ? (
          <AppText muted>
            Emergency: {[tenant.emergencyContactName, tenant.emergencyContactPhone].filter(Boolean).join(' · ')}
          </AppText>
        ) : null}
        {tenant.permanentAddress ? <AppText muted selectable>{tenant.permanentAddress}</AppText> : null}
      </Card>

      {tenancy ? <CurrentTenancy tenancy={tenancy} /> : (
        <Card>
          <AppText variant="heading">No bed</AppText>
          <AppText muted>{past.length > 0 ? 'This tenant has moved out.' : 'This tenant has not been given a bed yet.'}</AppText>
        </Card>
      )}

      {isOwner ? (
        <View style={styles.actions}>
          {tenancy ? (
            <>
              <Button label="Move to another bed" variant="secondary" onPress={() => go('/tenants/[id]/move')} />
              <Button label="Change rent or deposit" variant="secondary" onPress={() => go('/tenants/[id]/terms')} />
              <Button
                label={tenancy.state === 'Upcoming' ? 'Cancel booking' : 'Move out'}
                variant="secondary"
                onPress={() => go('/tenants/[id]/move-out')}
              />
            </>
          ) : (
            <Button label="Assign a bed" onPress={() => go('/tenants/[id]/move-in')} />
          )}
          <Button label="Edit details" variant="secondary" onPress={() => go('/tenants/[id]/edit')} />
          {!tenancy ? <Button label="Archive tenant" variant="secondary" onPress={confirmArchive} loading={archive.isPending} /> : null}
          <InlineError error={archive.error} action="archive this tenant" />
        </View>
      ) : null}

      <RentSection tenant={tenant} />

      {past.length > 0 ? (
        <Card>
          <AppText variant="heading">History</AppText>
          {past.map((h) => (
            <PastTenancy key={h.id} tenancy={h} />
          ))}
        </Card>
      ) : null}
    </FormScreen>
  );
}

function RentSection({ tenant }: { tenant: TenantDetail }) {
  const { colors } = useTheme();
  const charges = useCharges('All', tenant.id);
  const items = charges.data?.pages[0]?.items.slice(0, 6) ?? [];
  if (!tenant.currentTenancy && items.length === 0) return null;
  return (
    <>
      <Card>
        <AppText variant="heading">Rent</AppText>
        <Detail label="Owes" value={formatRupees(tenant.outstandingAmount)} />
        <Detail label="Overdue" value={formatRupees(tenant.overdueAmount)} />
        {tenant.overdueAmount > 0 ? (
          <AppText variant="caption" color={colors.danger}>
            Some rent is past its due date.
          </AppText>
        ) : null}
      </Card>
      {items.length > 0 ? <SectionHeader title="Recent dues" /> : null}
      <View style={styles.dues}>
        {items.map((c, i) => (
          <RentChargeCard key={c.id} charge={c} index={i} />
        ))}
      </View>
    </>
  );
}

function PhoneLink({ phone }: { phone: string }) {
  const { colors } = useTheme();
  return (
    <Pressable
      accessibilityRole="link"
      accessibilityLabel={`Call ${phone}`}
      onPress={() => void Linking.openURL(`tel:${phone.replace(/[^\d+]/g, '')}`)}
      style={styles.phone}
    >
      <AppText color={colors.primary}>{phone}</AppText>
    </Pressable>
  );
}

function CurrentTenancy({ tenancy }: { tenancy: Tenancy }) {
  const upcoming = tenancy.state === 'Upcoming';
  return (
    <Card>
      <View style={styles.row}>
        <AppText variant="heading" style={styles.flex}>
          Room {tenancy.roomNumber} · Bed {tenancy.bedLabel}
        </AppText>
        <StatusPill label={upcoming ? 'Moving in' : 'Living here'} tone={upcoming ? 'warning' : 'info'} />
      </View>
      <AppText muted>{tenancy.propertyName}</AppText>
      <Detail label="Rent" value={`${formatRupees(tenancy.monthlyRent)} / month`} />
      <Detail label="Due" value={`${ordinal(tenancy.rentDueDay)} of every month`} />
      <Detail label="Deposit" value={formatRupees(tenancy.securityDeposit)} />
      <Detail label={upcoming ? 'Moves in' : 'Moved in'} value={formatDate(tenancy.startDate)} />
    </Card>
  );
}

function PastTenancy({ tenancy }: { tenancy: Tenancy }) {
  const reason =
    tenancy.endReason === 'Cancelled' ? 'Booking cancelled' : tenancy.endReason === 'Transferred' ? 'Moved to another bed' : 'Moved out';
  const dates = tenancy.endReason === 'Cancelled'
    ? `Was due ${formatDate(tenancy.startDate)}`
    : `${formatDate(tenancy.startDate)} – ${tenancy.endDate ? formatDate(tenancy.endDate) : ''}`;
  return (
    <View style={styles.history}>
      <AppText>
        {tenancy.propertyName} · Room {tenancy.roomNumber} · Bed {tenancy.bedLabel}
      </AppText>
      <AppText variant="caption" muted>
        {dates} · {reason} · {formatRupees(tenancy.monthlyRent)}/month
      </AppText>
    </View>
  );
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.row} accessible accessibilityLabel={`${label}: ${value}`}>
      <AppText muted style={styles.label}>
        {label}
      </AppText>
      <AppText style={styles.flex}>{value}</AppText>
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  flex: { flex: 1 },
  label: { width: 80 },
  actions: { gap: spacing.sm },
  phone: { alignSelf: 'flex-start', paddingVertical: spacing.xs },
  history: { gap: 2, paddingVertical: spacing.xs },
  dues: { gap: spacing.md },
});
