import { useLocalSearchParams } from 'expo-router';

import { MoveInScreen } from '@/screens/tenants/TenancyScreens';

export default function Route() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <MoveInScreen tenantId={id} />;
}
