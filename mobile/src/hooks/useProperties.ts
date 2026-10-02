import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import {
  bedsApi,
  type BedInput,
  type CreateRoomInput,
  propertiesApi,
  type PropertyInput,
  roomsApi,
  type UpdateRoomInput,
} from '@/api/properties';

const PAGE_SIZE = 20;

export const propertyKeys = {
  /** Everything under properties/rooms/beds; invalidated after any change, since occupancy rolls up. */
  all: ['properties'] as const,
  list: (search: string, includeArchived: boolean) => ['properties', 'list', search, includeArchived] as const,
  detail: (id: string) => ['properties', 'detail', id] as const,
  rooms: (propertyId: string) => ['properties', 'rooms', propertyId] as const,
  room: (id: string) => ['properties', 'room', id] as const,
  bed: (id: string) => ['properties', 'bed', id] as const,
};

export function usePropertyList(search: string, includeArchived: boolean, enabled = true) {
  return useInfiniteQuery({
    enabled,
    queryKey: propertyKeys.list(search, includeArchived),
    queryFn: ({ pageParam, signal }) => propertiesApi.list({ page: pageParam, pageSize: PAGE_SIZE, search, includeArchived }, signal),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page < last.totalPages ? last.page + 1 : undefined),
  });
}

export const useProperty = (id: string) =>
  useQuery({ queryKey: propertyKeys.detail(id), queryFn: ({ signal }) => propertiesApi.get(id, signal) });

export const useRooms = (propertyId: string) =>
  useQuery({ queryKey: propertyKeys.rooms(propertyId), queryFn: ({ signal }) => propertiesApi.rooms(propertyId, signal) });

export const useRoom = (id: string) => useQuery({ queryKey: propertyKeys.room(id), queryFn: ({ signal }) => roomsApi.get(id, signal) });

export const useBed = (id: string) => useQuery({ queryKey: propertyKeys.bed(id), queryFn: ({ signal }) => bedsApi.get(id, signal) });

function usePropertyMutation<TInput, TResult>(mutationFn: (input: TInput) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: propertyKeys.all }),
  });
}

export const useCreateProperty = () => usePropertyMutation((input: PropertyInput) => propertiesApi.create(input));
export const useUpdateProperty = (id: string) => usePropertyMutation((input: PropertyInput) => propertiesApi.update(id, input));
export const useArchiveProperty = (id: string) => usePropertyMutation(() => propertiesApi.archive(id));
export const useRestoreProperty = (id: string) => usePropertyMutation(() => propertiesApi.restore(id));

export const useCreateRoom = (propertyId: string) =>
  usePropertyMutation((input: CreateRoomInput) => propertiesApi.createRoom(propertyId, input));
export const useUpdateRoom = (id: string) => usePropertyMutation((input: UpdateRoomInput) => roomsApi.update(id, input));
export const useArchiveRoom = (id: string) => usePropertyMutation(() => roomsApi.archive(id));
export const useAddBed = (roomId: string) =>
  usePropertyMutation((input: { label?: string; defaultMonthlyRent?: number }) => roomsApi.addBed(roomId, input));

export const useUpdateBed = (id: string) => usePropertyMutation((input: BedInput) => bedsApi.update(id, input));
export const useArchiveBed = (id: string) => usePropertyMutation(() => bedsApi.archive(id));
