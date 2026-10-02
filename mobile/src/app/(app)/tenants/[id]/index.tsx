import { useLocalSearchParams } from 'expo-router';

import { TenantDetailScreen } from '@/screens/tenants/TenantDetailScreen';

export default function Route() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <TenantDetailScreen tenantId={id} />;
}
