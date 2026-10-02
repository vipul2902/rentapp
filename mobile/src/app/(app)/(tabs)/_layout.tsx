import * as Haptics from 'expo-haptics';
import { Tabs } from 'expo-router';
import { type ColorValue, Platform } from 'react-native';

import { can } from '@/auth/permissions';
import { useCurrentUser } from '@/auth/SessionProvider';
import { Icon, type IconName } from '@/components/Icon';
import { useTheme } from '@/theme/useTheme';

function tabIcon(active: IconName, inactive: IconName) {
  return function TabIcon({ color, focused }: { color: ColorValue; focused: boolean }) {
    return <Icon name={focused ? active : inactive} size={24} color={color} />;
  };
}

/** Bottom tabs. Tabs the user has no permission for are hidden (the API enforces access regardless). */
export default function TabsLayout() {
  const { colors } = useTheme();
  const user = useCurrentUser();
  const seesRent = can(user, 'ViewTenants');
  const seesProperties = can(user, 'ViewProperties');

  return (
    <Tabs
      screenOptions={{
        tabBarActiveTintColor: colors.primary,
        tabBarInactiveTintColor: colors.textMuted,
        tabBarStyle: { backgroundColor: colors.surface, borderTopColor: colors.border },
        tabBarLabelStyle: { fontWeight: '600' },
        headerStyle: { backgroundColor: colors.background },
        headerShadowVisible: false,
        headerTitleStyle: { fontWeight: '700', color: colors.text },
      }}
      screenListeners={{
        tabPress: () => {
          if (Platform.OS !== 'web') void Haptics.selectionAsync().catch(() => undefined);
        },
      }}
    >
      <Tabs.Screen name="index" options={{ title: 'Home', headerShown: false, tabBarIcon: tabIcon('home', 'home-outline') }} />
      <Tabs.Screen name="rent" options={{ title: 'Rent', href: seesRent ? undefined : null, tabBarIcon: tabIcon('wallet', 'wallet-outline') }} />
      <Tabs.Screen name="tenants" options={{ title: 'Tenants', href: seesRent ? undefined : null, tabBarIcon: tabIcon('people', 'people-outline') }} />
      <Tabs.Screen name="properties" options={{ title: 'Properties', href: seesProperties ? undefined : null, tabBarIcon: tabIcon('business', 'business-outline') }} />
      <Tabs.Screen name="more" options={{ title: 'More', headerShown: false, tabBarIcon: tabIcon('grid', 'grid-outline') }} />
    </Tabs>
  );
}
