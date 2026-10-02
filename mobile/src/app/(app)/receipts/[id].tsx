import { useLocalSearchParams } from 'expo-router';

import { ReceiptScreen } from '@/screens/payments/ReceiptScreen';

export default function ReceiptRoute() {
  const { id, recorded } = useLocalSearchParams<{ id: string; recorded?: string }>();
  return <ReceiptScreen receiptId={id} justRecorded={recorded === '1'} />;
}
