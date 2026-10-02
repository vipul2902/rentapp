import { useLocalSearchParams } from 'expo-router';

import { AddBedScreen } from '@/screens/properties/BedFormScreen';

export default function NewBedRoute() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <AddBedScreen roomId={id} />;
}
