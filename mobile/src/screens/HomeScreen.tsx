import { router } from 'expo-router';
import { Linking, RefreshControl, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import type { RentCharge } from '@/api/rent';
import { can } from '@/auth/permissions';
import { useCurrentUser } from '@/auth/SessionProvider';
import { AppText } from '@/components/AppText';
import { Avatar } from '@/components/Avatar';
import { FadeIn } from '@/components/FadeIn';
import { Icon, type IconName } from '@/components/Icon';
import { LogoMark } from '@/components/Logo';
import { PressableScale } from '@/components/PressableScale';
import { EmptyState, GradientHero, SectionHeader, StatTile } from '@/components/Visuals';
import { usePropertyList } from '@/hooks/useProperties';
import { useGenerateCharges, useOverdue, useRentSummary } from '@/hooks/useRent';
import { elevation, radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';
import { formatRupees } from '@/utils/money';

/**
 * The owner's daily view (spec §14): how much is left to collect, who is overdue (with one-tap call),
 * how full the beds are, and the next thing to do.
 */
export function HomeScreen() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const user = useCurrentUser();
  const isOwner = user.role === 'Owner';
  const seesRent = can(user, 'ViewTenants');
  const seesBeds = can(user, 'ViewProperties');

  const summary = useRentSummary(seesRent);
  const overdue = useOverdue(seesRent);
  const properties = usePropertyList('', false, seesBeds);
  const generate = useGenerateCharges();

  const beds = (properties.data?.pages.flatMap((p) => p.items) ?? []).reduce(
    (sum, p) => ({ total: sum.total + p.occupancy.totalBeds, occupied: sum.occupied + p.occupancy.occupied, vacant: sum.vacant + p.occupancy.vacant }),
    { total: 0, occupied: 0, vacant: 0 },
  );

  const refreshing = summary.isRefetching || overdue.isRefetching || properties.isRefetching || generate.isPending;
  const refresh = () => {
    if (isOwner) generate.mutate();
    void summary.refetch();
    void overdue.refetch();
    void properties.refetch();
  };

  const s = summary.data;
  const firstName = user.name.split(' ')[0] ?? user.name;

  return (
    <ScrollView
      style={{ backgroundColor: colors.background }}
      contentContainerStyle={[styles.content, { paddingTop: insets.top + spacing.md, paddingBottom: insets.bottom + spacing.xl }]}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={refresh} tintColor={colors.primary} />}
    >
      <View style={styles.greeting}>
        <LogoMark size={44} />
        <View style={styles.flex}>
          <View style={styles.row}>
            <AppText variant="title">Hi, {firstName}</AppText>
            <AppText variant="title"> 👋</AppText>
          </View>
          <AppText muted numberOfLines={1}>
            {user.organization.name}
          </AppText>
        </View>
      </View>

      {seesRent ? (
        <FadeIn>
          <GradientHero>
            <AppText variant="overline" color="rgba(255,255,255,0.8)">
              Left to collect
            </AppText>
            <AppText variant="display" color="#FFFFFF" accessibilityLabel={`Left to collect ${formatRupees(s?.outstanding.amount ?? 0)}`}>
              {s ? formatRupees(s.outstanding.amount) : '…'}
            </AppText>
            <View style={styles.heroRow}>
              <HeroChip icon="alert-circle" label={`${formatRupees(s?.overdue.amount ?? 0)} overdue`} />
              <HeroChip icon="today" label={`${s?.dueToday.count ?? 0} due today`} />
            </View>
            <PressableScale accessibilityRole="button" accessibilityLabel="Open rent dues" onPress={() => router.push('/rent')} style={styles.heroLink}>
              <AppText variant="label" color="#FFFFFF">
                See all rent dues
              </AppText>
              <Icon name="arrow-forward" size={16} color="#FFFFFF" />
            </PressableScale>
          </GradientHero>
        </FadeIn>
      ) : null}

      <View style={styles.tiles}>
        {seesBeds ? (
          <>
            <StatTile icon="bed" tone="primary" value={`${beds.occupied}/${beds.total}`} label="Beds occupied" onPress={() => router.push('/properties')} />
            <StatTile icon="key" tone="success" value={String(beds.vacant)} label="Vacant beds" onPress={() => router.push('/properties')} />
          </>
        ) : null}
        {seesRent ? (
          <>
            <StatTile icon="calendar" tone="warning" value={formatRupees(s?.dueThisWeek.amount ?? 0)} label="Due this week" onPress={() => router.push({ pathname: '/rent', params: { filter: 'Upcoming' } })} />
            <StatTile icon="receipt" tone="info" value={formatRupees(s?.billedThisMonth.amount ?? 0)} label="Billed this month" onPress={() => router.push({ pathname: '/rent', params: { filter: 'All' } })} />
          </>
        ) : null}
      </View>

      {seesRent ? (
        <>
          <SectionHeader title="Overdue" actionLabel="See all" onAction={() => router.push({ pathname: '/rent', params: { filter: 'Overdue' } })} />
          {overdue.data && overdue.data.items.length === 0 ? (
            <EmptyState icon="happy-outline" title="Nobody is overdue" message="Every due date so far has been met." />
          ) : (
            <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.carousel}>
              {(overdue.data?.items ?? []).map((charge, i) => (
                <OverdueCard key={charge.id} charge={charge} index={i} />
              ))}
            </ScrollView>
          )}
        </>
      ) : null}

      {isOwner ? (
        <>
          <SectionHeader title="Quick actions" />
          <View style={styles.actions}>
            <QuickAction icon="person-add" label="Add tenant" onPress={() => router.push('/tenants/new')} />
            <QuickAction icon="business" label="Add property" onPress={() => router.push('/properties/new')} />
            <QuickAction icon="wallet" label="Rent dues" onPress={() => router.push('/rent')} />
            <QuickAction icon="people" label="Staff" onPress={() => router.push('/staff')} />
          </View>
        </>
      ) : null}
    </ScrollView>
  );
}

function HeroChip({ icon, label }: { icon: IconName; label: string }) {
  return (
    <View style={styles.heroChip}>
      <Icon name={icon} size={14} color="#FFFFFF" />
      <AppText variant="label" color="#FFFFFF">
        {label}
      </AppText>
    </View>
  );
}

/** Spec §13: overdue cards with Call / View tenant / Record payment. Remind arrives with reminders. */
function OverdueCard({ charge, index }: { charge: RentCharge; index: number }) {
  const { colors } = useTheme();
  const canRecord = can(useCurrentUser(), 'RecordPayments');
  return (
    <FadeIn delay={index * 60} from="right">
      <View style={[styles.overdue, { backgroundColor: colors.surface }, elevation(colors.shadow)]}>
        <PressableScale
          accessibilityRole="button"
          accessibilityLabel={`${charge.tenantName}, ${formatRupees(charge.balance)} overdue, ${charge.daysOverdue} days late`}
          onPress={() => router.push({ pathname: '/rent/[id]', params: { id: charge.id } })}
          style={styles.overdueTop}
        >
          <Avatar name={charge.tenantName} size={40} />
          <View style={styles.flex}>
            <AppText variant="label" numberOfLines={1}>
              {charge.tenantName}
            </AppText>
            <AppText variant="caption" muted numberOfLines={1}>
              Room {charge.roomNumber} · Bed {charge.bedLabel}
            </AppText>
          </View>
        </PressableScale>
        <AppText variant="heading" color={colors.danger}>
          {formatRupees(charge.balance)}
        </AppText>
        <AppText variant="caption" color={colors.danger}>
          {charge.daysOverdue} days late
        </AppText>
        <View style={styles.overdueActions}>
          <PressableScale
            accessibilityRole="button"
            accessibilityLabel={`Call ${charge.tenantName}`}
            onPress={() => void Linking.openURL(`tel:${charge.tenantPhone.replace(/[^\d+]/g, '')}`)}
            style={[styles.iconButton, { backgroundColor: colors.successSurface }]}
          >
            <Icon name="call" size={18} color={colors.success} />
          </PressableScale>
          <PressableScale
            accessibilityRole="button"
            accessibilityLabel={`View ${charge.tenantName}`}
            onPress={() => router.push({ pathname: '/tenants/[id]', params: { id: charge.tenantId } })}
            style={[styles.iconButton, { backgroundColor: colors.primarySoft }]}
          >
            <Icon name="person" size={18} color={colors.primary} />
          </PressableScale>
          {canRecord ? (
            <PressableScale
              accessibilityRole="button"
              accessibilityLabel={`Record payment from ${charge.tenantName}`}
              onPress={() => router.push({ pathname: '/payments/new', params: { tenantId: charge.tenantId, chargeId: charge.id } })}
              style={[styles.payButton, { backgroundColor: colors.primary }]}
            >
              <Icon name="cash" size={16} color={colors.onPrimary} />
              <AppText variant="label" color={colors.onPrimary}>
                Paid
              </AppText>
            </PressableScale>
          ) : null}
        </View>
      </View>
    </FadeIn>
  );
}

function QuickAction({ icon, label, onPress }: { icon: IconName; label: string; onPress: () => void }) {
  const { colors } = useTheme();
  return (
    <PressableScale accessibilityRole="button" accessibilityLabel={label} onPress={onPress} style={[styles.quick, { backgroundColor: colors.surface }, elevation(colors.shadow)]}>
      <View style={[styles.quickIcon, { backgroundColor: colors.accentSoft }]}>
        <Icon name={icon} size={22} color={colors.accent} />
      </View>
      <AppText variant="label" style={styles.center}>
        {label}
      </AppText>
    </PressableScale>
  );
}

const styles = StyleSheet.create({
  content: { paddingHorizontal: spacing.lg, gap: spacing.lg },
  flex: { flex: 1 },
  row: { flexDirection: 'row', alignItems: 'center' },
  center: { textAlign: 'center' },
  greeting: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  heroRow: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, marginTop: spacing.xs },
  heroChip: { flexDirection: 'row', alignItems: 'center', gap: 6, backgroundColor: 'rgba(255,255,255,0.18)', borderRadius: radius.pill, paddingHorizontal: spacing.md, paddingVertical: 6 },
  heroLink: { flexDirection: 'row', alignItems: 'center', gap: 6, marginTop: spacing.sm, alignSelf: 'flex-start', minHeight: 36 },
  tiles: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md },
  carousel: { gap: spacing.md, paddingVertical: spacing.xs, paddingRight: spacing.lg },
  overdue: { width: 220, borderRadius: radius.lg, padding: spacing.lg, gap: spacing.xs },
  overdueTop: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, marginBottom: spacing.xs },
  overdueActions: { flexDirection: 'row', gap: spacing.sm, marginTop: spacing.sm },
  iconButton: { width: 44, height: 44, borderRadius: 22, alignItems: 'center', justifyContent: 'center' },
  payButton: { flex: 1, height: 44, borderRadius: 22, flexDirection: 'row', gap: 6, alignItems: 'center', justifyContent: 'center' },
  actions: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md },
  quick: { flexBasis: '46%', flexGrow: 1, borderRadius: radius.lg, padding: spacing.lg, alignItems: 'center', gap: spacing.sm },
  quickIcon: { width: 48, height: 48, borderRadius: 16, alignItems: 'center', justifyContent: 'center' },
});
