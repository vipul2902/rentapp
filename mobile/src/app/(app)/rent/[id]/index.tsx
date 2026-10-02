import { useLocalSearchParams } from 'expo-router';

import { ChargeDetailScreen } from '@/screens/rent/ChargeDetailScreen';

export default function ChargeRoute() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <ChargeDetailScreen chargeId={id} />;
}
