import { useLocalSearchParams } from 'expo-router';

import { EditBedScreen } from '@/screens/properties/BedFormScreen';

export default function EditBedRoute() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <EditBedScreen bedId={id} />;
}
