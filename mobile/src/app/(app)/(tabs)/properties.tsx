import { Redirect } from 'expo-router';

import { can } from '@/auth/permissions';
import { useCurrentUser } from '@/auth/SessionProvider';
import { PropertyListScreen } from '@/screens/properties/PropertyListScreen';

/** Hidden tabs can still be reached by URL; send users without ViewProperties back home (the API refuses anyway). */
export default function PropertiesTab() {
  const user = useCurrentUser();
  if (!can(user, 'ViewProperties')) {
    return <Redirect href="/" />;
  }
  return <PropertyListScreen />;
}
