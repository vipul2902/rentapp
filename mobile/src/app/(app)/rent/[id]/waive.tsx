import { useLocalSearchParams } from 'expo-router';

import { WaiveScreen } from '@/screens/rent/ChargeDetailScreen';

export default function WaiveRoute() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <WaiveScreen chargeId={id} />;
}
