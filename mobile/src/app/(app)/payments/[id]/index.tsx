import { useLocalSearchParams } from 'expo-router';

import { PaymentDetailScreen } from '@/screens/payments/PaymentDetailScreen';

export default function PaymentRoute() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <PaymentDetailScreen paymentId={id} />;
}
