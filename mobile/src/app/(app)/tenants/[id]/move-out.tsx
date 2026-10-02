import { useLocalSearchParams } from 'expo-router';

import { MoveOutScreen } from '@/screens/tenants/TenancyScreens';

export default function Route() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <MoveOutScreen tenantId={id} />;
}
