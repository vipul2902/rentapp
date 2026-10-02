import { useLocalSearchParams } from 'expo-router';

import { RecordPaymentScreen } from '@/screens/payments/RecordPaymentScreen';

export default function RecordPaymentRoute() {
  const { tenantId, chargeId } = useLocalSearchParams<{ tenantId: string; chargeId?: string }>();
  return <RecordPaymentScreen tenantId={tenantId} chargeId={chargeId || undefined} />;
}
