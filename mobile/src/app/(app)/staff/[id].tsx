import { useLocalSearchParams } from 'expo-router';

import { StaffDetailScreen } from '@/screens/staff/StaffDetailScreen';

export default function StaffDetailRoute() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <StaffDetailScreen id={id} />;
}
