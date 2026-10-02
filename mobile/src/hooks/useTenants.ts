import { useInfiniteQuery, useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';

import { propertiesApi } from '@/api/properties';
import { type MoveInInput, type TenantDetailsInput, type TenantFilter, tenantsApi, type TermsInput } from '@/api/tenants';

import { propertyKeys } from './useProperties';

const PAGE_SIZE = 25;

export const tenantKeys = {
  all: ['tenants'] as const,
  list: (search: string, filter: TenantFilter) => ['tenants', 'list', search, filter] as const,
  detail: (id: string) => ['tenants', 'detail', id] as const,
};

export function useTenantList(search: string, filter: TenantFilter) {
  return useInfiniteQuery({
    queryKey: tenantKeys.list(search, filter),
    queryFn: ({ pageParam, signal }) => tenantsApi.list({ page: pageParam, pageSize: PAGE_SIZE, search, filter }, signal),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page < last.totalPages ? last.page + 1 : undefined),
  });
}

export const useTenant = (id: string) =>
  useQuery({ queryKey: tenantKeys.detail(id), queryFn: ({ signal }) => tenantsApi.get(id, signal) });

/** Tenant changes also change occupancy, so property/room caches are refreshed too. */
function useTenantMutation<TInput, TResult>(mutationFn: (input: TInput) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn,
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: tenantKeys.all }),
        queryClient.invalidateQueries({ queryKey: propertyKeys.all }),
        queryClient.invalidateQueries({ queryKey: ['rent'] }),
      ]);
    },
  });
}

export const useCreateTenant = () =>
  useTenantMutation((input: TenantDetailsInput & { moveIn?: MoveInInput }) => tenantsApi.create(input));
export const useUpdateTenant = (id: string) => useTenantMutation((input: TenantDetailsInput) => tenantsApi.update(id, input));
export const useArchiveTenant = (id: string) => useTenantMutation(() => tenantsApi.archive(id));
export const useMoveIn = (id: string) => useTenantMutation((input: MoveInInput) => tenantsApi.moveIn(id, input));
export const useMoveOut = (id: string) => useTenantMutation((date: string) => tenantsApi.moveOut(id, date));
export const useMoveTenant = (id: string) =>
  useTenantMutation((input: { bedId: string; moveDate: string; monthlyRent?: number }) => tenantsApi.move(id, input));
export const useUpdateTerms = (id: string) => useTenantMutation((input: TermsInput) => tenantsApi.updateTerms(id, input));

export interface VacantBed {
  bedId: string;
  propertyId: string;
  propertyName: string;
  roomNumber: string;
  label: string;
  defaultMonthlyRent: number | null;
}

/**
 * Beds a tenant can be assigned to: vacant, or held by a manual reservation (not by another tenant's
 * booking), in rooms that are in use. The server re-checks everything when assigning.
 */
export function useVacantBeds(propertyId?: string) {
  const properties = useQuery({
    queryKey: [...propertyKeys.all, 'picker'],
    queryFn: ({ signal }) => propertiesApi.list({ page: 1, pageSize: 100 }, signal),
  });
  const ids = propertyId ? [propertyId] : (properties.data?.items.map((p) => p.id) ?? []);
  const names = new Map(properties.data?.items.map((p) => [p.id, p.name]));

  const rooms = useQueries({
    queries: ids.map((pid) => ({
      queryKey: propertyKeys.rooms(pid),
      queryFn: ({ signal }: { signal: AbortSignal }) => propertiesApi.rooms(pid, signal),
    })),
  });

  const beds: VacantBed[] = [];
  rooms.forEach((query, i) => {
    const pid = ids[i] ?? '';
    for (const room of query.data ?? []) {
      if (room.status !== 'Active') continue;
      for (const bed of room.beds) {
        const free = bed.occupancy === 'Vacant' || (bed.occupancy === 'Reserved' && bed.status === 'Reserved' && !bed.tenant);
        if (free) {
          beds.push({
            bedId: bed.id,
            propertyId: pid,
            propertyName: names.get(pid) ?? '',
            roomNumber: room.roomNumber,
            label: bed.label,
            defaultMonthlyRent: bed.defaultMonthlyRent,
          });
        }
      }
    }
  });

  return {
    beds,
    isPending: properties.isPending || rooms.some((r) => r.isPending),
    error: properties.error ?? rooms.find((r) => r.error)?.error ?? null,
    refetch: () => {
      void properties.refetch();
      rooms.forEach((r) => void r.refetch());
    },
  };
}
