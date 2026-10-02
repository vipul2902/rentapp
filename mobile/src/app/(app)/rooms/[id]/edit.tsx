import { useLocalSearchParams } from 'expo-router';

import { EditRoomScreen } from '@/screens/properties/RoomFormScreen';

export default function EditRoomRoute() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <EditRoomScreen roomId={id} />;
}
