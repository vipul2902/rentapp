import { useLocalSearchParams } from 'expo-router';

import { ComposeReminderScreen } from '@/screens/reminders/ReminderScreens';

export default function ComposeReminderRoute() {
  const { chargeId } = useLocalSearchParams<{ chargeId: string }>();
  return <ComposeReminderScreen chargeId={chargeId} />;
}
