import { useLocalSearchParams } from 'expo-router';

import { EditTenantScreen } from '@/screens/tenants/TenantFormScreen';

export default function Route() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <EditTenantScreen tenantId={id} />;
}
