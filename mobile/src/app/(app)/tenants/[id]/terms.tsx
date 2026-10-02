import { useLocalSearchParams } from 'expo-router';

import { TenancyTermsScreen } from '@/screens/tenants/TenancyScreens';

export default function Route() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <TenancyTermsScreen tenantId={id} />;
}
