import { Redirect } from 'expo-router';

import { can } from '@/auth/permissions';
import { useCurrentUser } from '@/auth/SessionProvider';
import { RentScreen } from '@/screens/rent/RentScreen';

/** Hidden tabs can still be reached by URL; send users without ViewTenants back home (the API refuses anyway). */
export default function RentTab() {
  const user = useCurrentUser();
  if (!can(user, 'ViewTenants')) {
    return <Redirect href="/" />;
  }
  return <RentScreen />;
}
