import { Redirect } from 'expo-router';

import { can } from '@/auth/permissions';
import { useCurrentUser } from '@/auth/SessionProvider';
import { TenantListScreen } from '@/screens/tenants/TenantListScreen';

/** Hidden tabs can still be reached by URL; send users without ViewTenants back home (the API refuses anyway). */
export default function TenantsTab() {
  const user = useCurrentUser();
  if (!can(user, 'ViewTenants')) {
    return <Redirect href="/" />;
  }
  return <TenantListScreen />;
}
