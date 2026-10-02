import { router } from 'expo-router';
import { useState } from 'react';
import { RefreshControl, StyleSheet, View } from 'react-native';

import type { Reminder, ReminderChannel, ReminderPreview, ReminderSuggestion, ReminderType } from '@/api/reminders';
import { AppText } from '@/components/AppText';
import { Avatar } from '@/components/Avatar';
import { Card } from '@/components/Card';
import { ErrorState } from '@/components/ErrorState';
import { FadeIn } from '@/components/FadeIn';
import { FormScreen } from '@/components/FormScreen';
import { Icon, type IconName } from '@/components/Icon';
import { InlineError } from '@/components/InlineError';
import { PressableScale } from '@/components/PressableScale';
import { SegmentedControl } from '@/components/SegmentedControl';
import { StatusPill } from '@/components/StatusPill';
import { TextField } from '@/components/TextField';
import { ChipBar, EmptyState, SkeletonList } from '@/components/Visuals';
import {
  CHANNEL_LABEL,
  REMINDER_TYPE,
  useMarkReminderSent,
  useReminderHistory,
  useReminderPreview,
  useReminderQueue,
  useSendReminder,
} from '@/hooks/useReminders';
import { periodLabel } from '@/hooks/useRent';
import { elevation, radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';
import { formatDate } from '@/utils/dates';
import { formatRupees } from '@/utils/money';

const CHANNELS: readonly { channel: ReminderChannel; label: string; icon: IconName }[] = [
  { channel: 'WhatsApp', label: 'WhatsApp', icon: 'logo-whatsapp' },
  { channel: 'Sms', label: 'SMS', icon: 'chatbubble-outline' },
  { channel: 'Share', label: 'Share', icon: 'share-social-outline' },
  { channel: 'Copy', label: 'Copy', icon: 'copy-outline' },
];

/** WhatsApp / SMS / Share / Copy. Each opens the app with the message ready; the person presses Send. */
function SendButtons({ onSend, busy, name }: { onSend: (channel: ReminderChannel) => void; busy: boolean; name: string }) {
  const { colors } = useTheme();
  return (
    <View style={styles.sendRow}>
      {CHANNELS.map((c) => (
        <PressableScale
          key={c.channel}
          accessibilityRole="button"
          accessibilityLabel={`${c.channel === 'Copy' ? 'Copy reminder for' : `Send reminder by ${c.label} to`} ${name}`}
          accessibilityState={{ disabled: busy }}
          disabled={busy}
          onPress={() => onSend(c.channel)}
          style={[styles.sendButton, { backgroundColor: c.channel === 'WhatsApp' ? colors.successSurface : colors.primarySoft, opacity: busy ? 0.6 : 1 }]}
        >
          <Icon name={c.icon} size={18} color={c.channel === 'WhatsApp' ? colors.success : colors.primary} />
          <AppText variant="caption" color={c.channel === 'WhatsApp' ? colors.success : colors.primary}>
            {c.label}
          </AppText>
        </PressableScale>
      ))}
    </View>
  );
}

// ---- Queue and history ----------------------------------------------------------------------------

type Filter = 'All' | ReminderType;

/** Who to remind today (spec §6 stages), with ready messages, and everything sent before. */
export function RemindersScreen() {
  const [tab, setTab] = useState<'queue' | 'history'>('queue');
  return tab === 'queue' ? <QueueView tab={tab} setTab={setTab} /> : <HistoryView tab={tab} setTab={setTab} />;
}

function Tabs({ tab, setTab }: { tab: 'queue' | 'history'; setTab: (t: 'queue' | 'history') => void }) {
  return (
    <SegmentedControl
      label="Reminders"
      options={[
        { value: 'queue', label: 'To send' },
        { value: 'history', label: 'History' },
      ]}
      value={tab}
      onChange={setTab}
    />
  );
}

function QueueView(props: { tab: 'queue' | 'history'; setTab: (t: 'queue' | 'history') => void }) {
  const query = useReminderQueue();
  const send = useSendReminder();
  const [filter, setFilter] = useState<Filter>('All');
  const q = query.data;
  const items = (q?.items ?? []).filter((s) => filter === 'All' || s.type === filter);

  return (
    <FormScreen refreshControl={<RefreshControl refreshing={query.isRefetching} onRefresh={() => void query.refetch()} />}>
      <Tabs {...props} />
      {q ? (
        <ChipBar
          label="Show"
          value={filter}
          onChange={setFilter}
          options={[
            { value: 'All', label: 'All', icon: 'list-outline', count: q.items.length },
            { value: 'LongOverdue', label: '7+ days late', icon: 'flame-outline', count: q.longOverdue },
            { value: 'Overdue', label: 'Overdue', icon: 'alert-circle-outline', count: q.overdue },
            { value: 'DueToday', label: 'Due today', icon: 'today-outline', count: q.dueToday },
            { value: 'Upcoming', label: 'Upcoming', icon: 'time-outline', count: q.upcoming },
          ]}
        />
      ) : null}
      <InlineError error={send.error} action="send the reminder" />
      {query.isPending ? (
        <SkeletonList rows={3} />
      ) : query.error ? (
        <ErrorState error={query.error} action="load reminders" onRetry={() => void query.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState icon="checkmark-done-circle-outline" title="All caught up" message="No reminders need sending right now." />
      ) : (
        items.map((s, i) => (
          <FadeIn key={`${s.charge.id}-${s.type}`} delay={Math.min(i, 8) * 40}>
            <SuggestionCard
              suggestion={s}
              busy={send.isPending}
              onSend={(channel) => send.mutate({ chargeId: s.charge.id, phone: s.charge.tenantPhone, message: s.message, channel })}
            />
          </FadeIn>
        ))
      )}
      <AppText variant="caption" muted style={styles.center}>
        Each stage is suggested once: 3 days before, on the due date, 3 days late, then every week after 7 days.
      </AppText>
    </FormScreen>
  );
}

function SuggestionCard({ suggestion: s, busy, onSend }: { suggestion: ReminderSuggestion; busy: boolean; onSend: (c: ReminderChannel) => void }) {
  const { colors } = useTheme();
  const type = REMINDER_TYPE[s.type];
  const c = s.charge;
  return (
    <View style={[styles.card, { backgroundColor: colors.surface }, elevation(colors.shadow)]}>
      <PressableScale
        accessibilityRole="button"
        accessibilityLabel={`${c.tenantName}, ${formatRupees(c.balance)}, ${type.label}. Edit message`}
        onPress={() => router.push({ pathname: '/reminders/new', params: { chargeId: c.id } })}
        style={styles.cardTop}
      >
        <Avatar name={c.tenantName} size={40} />
        <View style={styles.flex}>
          <AppText variant="heading" numberOfLines={1}>
            {c.tenantName}
          </AppText>
          <AppText variant="caption" muted numberOfLines={1}>
            {formatRupees(c.balance)} · {periodLabel(c.periodStart)} · Room {c.roomNumber}
          </AppText>
        </View>
        <StatusPill label={type.label} tone={type.tone} icon={type.icon} />
      </PressableScale>
      <AppText muted numberOfLines={3}>
        {s.message}
      </AppText>
      <SendButtons onSend={onSend} busy={busy} name={c.tenantName} />
    </View>
  );
}

function HistoryView(props: { tab: 'queue' | 'history'; setTab: (t: 'queue' | 'history') => void }) {
  const query = useReminderHistory();
  const items = query.data?.pages.flatMap((p) => p.items) ?? [];
  return (
    <FormScreen refreshControl={<RefreshControl refreshing={query.isRefetching} onRefresh={() => void query.refetch()} />}>
      <Tabs {...props} />
      {query.isPending ? (
        <SkeletonList rows={3} />
      ) : query.error ? (
        <ErrorState error={query.error} action="load reminder history" onRetry={() => void query.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState icon="notifications-outline" title="No reminders yet" message="Reminders you send appear here." />
      ) : (
        <ReminderHistoryList reminders={items} showTenant />
      )}
      {query.hasNextPage ? (
        <PressableScale accessibilityRole="button" accessibilityLabel="Show more reminders" onPress={() => void query.fetchNextPage()} style={styles.more}>
          <AppText variant="label">Show more</AppText>
        </PressableScale>
      ) : null}
    </FormScreen>
  );
}

/** Past reminders, with "Mark as sent" for ones that were only copied. Also used on the tenant screen. */
export function ReminderHistoryList({ reminders, showTenant = false }: { reminders: Reminder[]; showTenant?: boolean }) {
  const { colors } = useTheme();
  const markSent = useMarkReminderSent();
  return (
    <View style={styles.history}>
      <InlineError error={markSent.error} action="mark the reminder as sent" />
      {reminders.map((r) => {
        const type = REMINDER_TYPE[r.type];
        const sent = r.status === 'Sent';
        return (
          <Card key={r.id}>
            <View style={styles.row}>
              <View style={styles.flex}>
                <AppText variant="label">
                  {showTenant ? `${r.tenantName} · ` : ''}
                  {periodLabel(r.periodStart)}
                </AppText>
                <AppText variant="caption" muted>
                  {type.label} · {CHANNEL_LABEL[r.channel]} · {formatDate(r.createdAt.slice(0, 10))}
                  {r.createdByName ? ` · ${r.createdByName}` : ''}
                </AppText>
              </View>
              <StatusPill label={sent ? 'Sent' : 'Not sent yet'} tone={sent ? 'success' : 'warning'} icon={sent ? 'checkmark-circle' : 'time'} />
            </View>
            <AppText muted numberOfLines={2}>
              {r.message}
            </AppText>
            {!sent ? (
              <PressableScale
                accessibilityRole="button"
                accessibilityLabel={`Mark reminder to ${r.tenantName} as sent`}
                onPress={() => markSent.mutate(r.id)}
                style={[styles.markSent, { backgroundColor: colors.primarySoft }]}
              >
                <Icon name="checkmark-done" size={16} color={colors.primary} />
                <AppText variant="label" color={colors.primary}>
                  Mark as sent
                </AppText>
              </PressableScale>
            ) : null}
          </Card>
        );
      })}
    </View>
  );
}

// ---- Compose -------------------------------------------------------------------------------------

/** One due: the suggested message (editable), the send buttons and earlier reminders for this due. */
export function ComposeReminderScreen({ chargeId }: { chargeId: string }) {
  const query = useReminderPreview(chargeId);
  if (query.isPending) {
    return (
      <FormScreen>
        <SkeletonList rows={2} />
      </FormScreen>
    );
  }
  if (query.error) {
    return (
      <FormScreen>
        <ErrorState error={query.error} action="prepare the reminder" onRetry={() => void query.refetch()} />
      </FormScreen>
    );
  }
  return <Composer preview={query.data} />;
}

function Composer({ preview }: { preview: ReminderPreview }) {
  const { colors } = useTheme();
  const c = preview.charge;
  const type = REMINDER_TYPE[preview.type];
  const [message, setMessage] = useState(preview.message);
  const [error, setError] = useState<string>();
  const [copied, setCopied] = useState<Reminder | null>(null);
  const send = useSendReminder();
  const markSent = useMarkReminderSent();

  const onSend = (channel: ReminderChannel) => {
    if (message.trim().length < 5) {
      setError('Write a message first.');
      return;
    }
    setError(undefined);
    send.mutate(
      { chargeId: c.id, phone: c.tenantPhone, message: message.trim(), channel },
      {
        onSuccess: (reminder) => {
          if (!reminder) return; // Backed out of the share sheet.
          if (reminder.status === 'Prepared') setCopied(reminder);
          else router.back();
        },
      },
    );
  };

  return (
    <FormScreen>
      <View style={styles.cardTop}>
        <Avatar name={c.tenantName} size={44} />
        <View style={styles.flex}>
          <AppText variant="heading">{c.tenantName}</AppText>
          <AppText muted>
            {formatRupees(c.balance)} · {periodLabel(c.periodStart)} · due {formatDate(c.dueDate)}
          </AppText>
        </View>
        <StatusPill label={type.label} tone={type.tone} icon={type.icon} />
      </View>

      <TextField label="Message" value={message} onChangeText={setMessage} multiline maxLength={1000} error={error} />
      <InlineError error={send.error ?? markSent.error} action="send the reminder" />

      {copied ? (
        <View style={[styles.copied, { backgroundColor: colors.successSurface }]} accessibilityRole="alert">
          <AppText variant="label" color={colors.success}>
            Copied. Paste it in any chat, then mark it as sent.
          </AppText>
          <PressableScale
            accessibilityRole="button"
            accessibilityLabel="Mark as sent"
            onPress={() => markSent.mutate(copied.id, { onSuccess: () => router.back() })}
            style={[styles.markSent, { backgroundColor: colors.surface }]}
          >
            <Icon name="checkmark-done" size={16} color={colors.success} />
            <AppText variant="label" color={colors.success}>
              Mark as sent
            </AppText>
          </PressableScale>
        </View>
      ) : null}

      <SendButtons onSend={onSend} busy={send.isPending} name={c.tenantName} />
      <AppText variant="caption" muted style={styles.center}>
        WhatsApp and SMS open with the message ready. You press Send; nothing is sent automatically.
      </AppText>

      {preview.history.length > 0 ? (
        <>
          <AppText variant="heading">Sent before</AppText>
          <ReminderHistoryList reminders={preview.history} />
        </>
      ) : null}
    </FormScreen>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  center: { textAlign: 'center' },
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  card: { borderRadius: radius.lg, padding: spacing.lg, gap: spacing.md },
  cardTop: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  sendRow: { flexDirection: 'row', gap: spacing.sm },
  sendButton: { flex: 1, minHeight: 52, borderRadius: radius.md, alignItems: 'center', justifyContent: 'center', gap: 2 },
  history: { gap: spacing.md },
  markSent: { flexDirection: 'row', alignItems: 'center', gap: 6, alignSelf: 'flex-start', borderRadius: radius.pill, paddingHorizontal: spacing.md, paddingVertical: spacing.xs + 2 },
  copied: { borderRadius: radius.md, padding: spacing.md, gap: spacing.sm },
  more: { alignSelf: 'center', padding: spacing.md },
});
