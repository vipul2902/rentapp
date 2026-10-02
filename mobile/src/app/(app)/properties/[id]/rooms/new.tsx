import { useLocalSearchParams } from 'expo-router';

import { CreateRoomScreen } from '@/screens/properties/RoomFormScreen';

export default function NewRoomRoute() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <CreateRoomScreen propertyId={id} />;
}
