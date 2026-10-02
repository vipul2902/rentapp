import { useLocalSearchParams } from 'expo-router';

import { MoveTenantScreen } from '@/screens/tenants/TenancyScreens';

export default function Route() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <MoveTenantScreen tenantId={id} />;
}
