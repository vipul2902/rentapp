import { useLocalSearchParams } from 'expo-router';

import { PropertyFormScreen } from '@/screens/properties/PropertyFormScreen';

export default function EditPropertyRoute() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <PropertyFormScreen propertyId={id} />;
}
