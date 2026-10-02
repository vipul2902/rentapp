import { useLocalSearchParams } from 'expo-router';

import { PropertyDetailScreen } from '@/screens/properties/PropertyDetailScreen';

export default function PropertyRoute() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <PropertyDetailScreen propertyId={id} />;
}
