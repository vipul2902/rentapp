import { router } from 'expo-router';
import { Linking, RefreshControl, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import type { Dashboard } from '@/api/dashboard';
import type { RentCharge } from '@/api/rent';
import { can } from '@/auth/permissions';
import { useCurrentUser } from '@/auth/SessionProvider';
import { AppText } from '@/components/AppText';
import { Avatar } from '@/components/Avatar';
import { ErrorState } from '@/components/ErrorState';
import { FadeIn } from '@/components/FadeIn';
import { Icon, type IconName } from '@/components/Icon';
import { LogoMark } from '@/components/Logo';
import { PressableScale } from '@/components/PressableScale';
import { EmptyState, GradientHero, SectionHeader, SkeletonList, StatTile } from '@/components/Visuals';
import { periodLabel, useDashboard, useGenerateCharges } from '@/hooks/useRent';
import { PaymentRow } from '@/screens/payments/PaymentDetailScreen';
import { elevation, radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';
import { formatRupees } from '@/utils/money';

/**
 * The owner's daily view (spec §14), from one dashboard request: what came in this month, how much of
 * this month's rent is collected, who is overdue (call or record payment in one tap), how full the beds
 * are, and quick actions. Each section only appears for users allowed to see it.
 */
export function HomeScreen() {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const user = useCurrentUser();
  const isOwner = user.role === 'Owner';
  const canRecord = can(user, 'RecordPayments') && can(user, 'ViewTenants');
  const canRemind = can(user, 'SendReminders');
  const dashboard = useDashboard();
  const generate = useGenerateCharges();

  const refresh = () => {
    // Owners also make sure any newly due rent exists; generating refreshes the dashboard when it finishes.
    if (isOwner) generate.mutate();
    else void dashboard.refetch();
  };

  const d = dashboard.data;
  const firstName = user.name.split(' ')[0] ?? user.name;

  return (
    <ScrollView
      style={{ backgroundColor: colors.background }}
      contentContainerStyle={[styles.content, { paddingTop: insets.top + spacing.md, paddingBottom: insets.bottom + spacing.xl }]}
      refreshControl={<RefreshControl refreshing={dashboard.isRefetching || generate.isPending} onRefresh={refresh} tintColor={colors.primary} />}
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

      {dashboard.isPending ? (
        <SkeletonList rows={3} />
      ) : dashboard.error ? (
        <ErrorState error={dashboard.error} action="load your dashboard" onRetry={() => void dashboard.refetch()} retrying={dashboard.isFetching} />
      ) : d ? (
        <DashboardBody dashboard={d} isOwner={isOwner} canRecord={canRecord} canRemind={canRemind} />
      ) : null}
    </ScrollView>
  );
}

function DashboardBody({ dashboard: d, isOwner, canRecord, canRemind }: { dashboard: Dashboard; isOwner: boolean; canRecord: boolean; canRemind: boolean }) {
  const rent = d.rent;
  const beds = d.occupancy?.beds;
  const monthName = rent ? (periodLabel(rent.thisMonth.month).split(' ')[0] ?? '') : '';

  return (
    <>
      {rent ? (
        <FadeIn>
          <GradientHero>
            <AppText variant="overline" color="rgba(255,255,255,0.8)">
              Collected in {monthName}
            </AppText>
            <AppText variant="display" color="#FFFFFF" accessibilityLabel={`Collected in ${monthName}: ${formatRupees(rent.collectedThisMonth.amount)}`}>
              {formatRupees(rent.collectedThisMonth.amount)}
            </AppText>
            {rent.thisMonth.expected > 0 ? (
              <View
                style={styles.progress}
                accessible
                accessibilityLabel={`${rent.thisMonth.percentCollected}% of ${monthName} rent collected: ${formatRupees(rent.thisMonth.collected)} of ${formatRupees(rent.thisMonth.expected)}`}
              >
                <View style={styles.progressTrack}>
                  <View style={[styles.progressFill, { width: `${rent.thisMonth.percentCollected}%` }]} />
                </View>
                <AppText variant="caption" color="rgba(255,255,255,0.9)">
                  {rent.thisMonth.percentCollected}% of {monthName} rent · {formatRupees(rent.thisMonth.collected)} of {formatRupees(rent.thisMonth.expected)}
                </AppText>
              </View>
            ) : null}
            <View style={styles.heroRow}>
              <HeroChip icon="alert-circle" label={`${formatRupees(rent.overdue.amount)} overdue`} />
              <HeroChip icon="today" label={`${rent.dueToday.count} due today`} />
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
        {rent ? (
          <>
            <StatTile icon="wallet" tone="danger" value={formatRupees(rent.outstanding.amount)} label="Left to collect" onPress={() => router.push({ pathname: '/rent', params: { filter: 'Outstanding' } })} />
            <StatTile icon="calendar" tone="warning" value={formatRupees(rent.dueThisWeek.amount)} label="Due this week" onPress={() => router.push({ pathname: '/rent', params: { filter: 'Upcoming' } })} />
          </>
        ) : null}
        {beds ? (
          <>
            <StatTile icon="bed" tone="primary" value={`${beds.occupied}/${beds.totalBeds}`} label="Beds occupied" onPress={() => router.push('/properties')} />
            <StatTile icon="key" tone="success" value={String(beds.vacant)} label="Vacant beds" onPress={() => router.push('/properties')} />
          </>
        ) : null}
      </View>

      {canRecord || canRemind || isOwner ? (
        <>
          <SectionHeader title="Quick actions" />
          <View style={styles.actions}>
            {canRecord ? <QuickAction icon="cash" label="Record payment" onPress={() => router.push('/payments/new')} /> : null}
            {canRemind ? (
              <QuickAction
                icon="notifications"
                label={d.remindersToSend ? `Reminders (${d.remindersToSend})` : 'Reminders'}
                onPress={() => router.push('/reminders')}
              />
            ) : null}
            {rent ? <QuickAction icon="alert-circle" label="Overdue" onPress={() => router.push({ pathname: '/rent', params: { filter: 'Overdue' } })} /> : null}
            {isOwner ? <QuickAction icon="person-add" label="Add tenant" onPress={() => router.push('/tenants/new')} /> : null}
            {isOwner ? <QuickAction icon="business" label="Add property" onPress={() => router.push('/properties/new')} /> : null}
          </View>
        </>
      ) : null}

      {rent ? (
        <>
          <SectionHeader title="Overdue" actionLabel="See all" onAction={() => router.push({ pathname: '/rent', params: { filter: 'Overdue' } })} />
          {rent.overdueList.length === 0 ? (
            <EmptyState icon="happy-outline" title="Nobody is overdue" message="Every due date so far has been met." />
          ) : (
            <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.carousel}>
              {rent.overdueList.map((charge, i) => (
                <OverdueCard key={charge.id} charge={charge} index={i} canRecord={canRecord} canRemind={canRemind} />
              ))}
            </ScrollView>
          )}

          {rent.recentPayments.length > 0 ? (
            <>
              <SectionHeader title="Recent payments" />
              <RecentPayments dashboard={d} />
            </>
          ) : null}
        </>
      ) : null}

      {!rent && !beds && !canRecord ? (
        <EmptyState icon="lock-closed-outline" title="Nothing to show yet" message="Ask the owner to give you access to properties or tenants." />
      ) : null}
    </>
  );
}

function RecentPayments({ dashboard }: { dashboard: Dashboard }) {
  const { colors } = useTheme();
  return (
    <View style={[styles.list, { backgroundColor: colors.surface }, elevation(colors.shadow)]}>
      {(dashboard.rent?.recentPayments ?? []).map((p) => (
        <PaymentRow key={p.id} payment={p} subtitlePrefix={p.tenantName} />
      ))}
    </View>
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

/** Spec §13: overdue cards with Call / View tenant / Remind / Record payment. */
function OverdueCard({ charge, index, canRecord, canRemind }: { charge: RentCharge; index: number; canRecord: boolean; canRemind: boolean }) {
  const { colors } = useTheme();
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
          {charge.daysOverdue} days late · {periodLabel(charge.periodStart)}
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
          {canRemind ? (
            <PressableScale
              accessibilityRole="button"
              accessibilityLabel={`Remind ${charge.tenantName}`}
              onPress={() => router.push({ pathname: '/reminders/new', params: { chargeId: charge.id } })}
              style={[styles.iconButton, { backgroundColor: colors.warningSurface }]}
            >
              <Icon name="notifications" size={18} color={colors.warning} />
            </PressableScale>
          ) : null}
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
  progress: { gap: 6, marginTop: spacing.xs },
  progressTrack: { height: 8, borderRadius: 4, backgroundColor: 'rgba(255,255,255,0.25)', overflow: 'hidden' },
  progressFill: { height: 8, borderRadius: 4, backgroundColor: '#FFFFFF' },
  heroRow: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, marginTop: spacing.xs },
  heroChip: { flexDirection: 'row', alignItems: 'center', gap: 6, backgroundColor: 'rgba(255,255,255,0.18)', borderRadius: radius.pill, paddingHorizontal: spacing.md, paddingVertical: 6 },
  heroLink: { flexDirection: 'row', alignItems: 'center', gap: 6, marginTop: spacing.sm, alignSelf: 'flex-start', minHeight: 36 },
  tiles: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md },
  carousel: { gap: spacing.md, paddingVertical: spacing.xs, paddingRight: spacing.lg },
  overdue: { width: 260, borderRadius: radius.lg, padding: spacing.lg, gap: spacing.xs },
  overdueTop: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, marginBottom: spacing.xs },
  overdueActions: { flexDirection: 'row', gap: spacing.sm, marginTop: spacing.sm },
  iconButton: { width: 44, height: 44, borderRadius: 22, alignItems: 'center', justifyContent: 'center' },
  payButton: { flex: 1, height: 44, borderRadius: 22, flexDirection: 'row', gap: 6, alignItems: 'center', justifyContent: 'center' },
  actions: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md },
  quick: { flexBasis: '46%', flexGrow: 1, borderRadius: radius.lg, padding: spacing.lg, alignItems: 'center', gap: spacing.sm },
  quickIcon: { width: 48, height: 48, borderRadius: 16, alignItems: 'center', justifyContent: 'center' },
  list: { borderRadius: radius.lg, overflow: 'hidden' },
});
