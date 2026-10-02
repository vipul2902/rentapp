import { useLocalSearchParams } from 'expo-router';

import { RoomDetailScreen } from '@/screens/properties/RoomDetailScreen';

export default function RoomRoute() {
  const { id } = useLocalSearchParams<{ id: string }>();
  return <RoomDetailScreen roomId={id} />;
}
