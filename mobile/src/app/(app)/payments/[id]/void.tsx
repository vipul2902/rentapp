import { useLocalSearchParams } from 'expo-router';

import { VoidPaymentScreen } from '@/screens/payments/PaymentDetailScreen';

export default function VoidPaymentRoute() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <VoidPaymentScreen paymentId={id} />;
}
