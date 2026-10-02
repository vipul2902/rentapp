import { useLocalSearchParams } from 'expo-router';

import { CreateTenantScreen } from '@/screens/tenants/TenantFormScreen';

export default function NewTenantRoute() {
  const { bedId } = useLocalSearchParams<{ bedId?: string }>();
  return <CreateTenantScreen bedId={bedId} />;
}
