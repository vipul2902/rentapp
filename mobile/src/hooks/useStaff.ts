import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import type { StaffMember, StaffPermission } from '@/api/types';
import { type CreateStaffInput, usersApi } from '@/api/users';

const PAGE_SIZE = 25;

export const staffKeys = {
  all: ['users'] as const,
  list: (search: string) => ['users', 'list', search] as const,
  detail: (id: string) => ['users', 'detail', id] as const,
};

export function useStaffList(search: string) {
  return useInfiniteQuery({
    queryKey: staffKeys.list(search),
    queryFn: ({ pageParam, signal }) => usersApi.list({ page: pageParam, pageSize: PAGE_SIZE, search }, signal),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page < last.totalPages ? last.page + 1 : undefined),
  });
}

export function useStaffMember(id: string) {
  return useQuery({
    queryKey: staffKeys.detail(id),
    queryFn: ({ signal }) => usersApi.get(id, signal),
  });
}

/** Every staff change refreshes both the list and the detail cache. */
function useStaffMutation<TInput>(mutationFn: (input: TInput) => Promise<StaffMember | void>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn,
    onSuccess: (result) => {
      if (result) {
        queryClient.setQueryData(staffKeys.detail(result.id), result);
      }
      return queryClient.invalidateQueries({ queryKey: staffKeys.all });
    },
  });
}

export const useCreateStaff = () => useStaffMutation((input: CreateStaffInput) => usersApi.create(input));

export const useUpdatePermissions = (id: string) =>
  useStaffMutation((permissions: StaffPermission[]) => usersApi.updatePermissions(id, permissions));

export const useSetStaffActive = (id: string) =>
  useStaffMutation((active: boolean) => (active ? usersApi.enable(id) : usersApi.disable(id)));

export const useResetStaffPassword = (id: string) => useStaffMutation((password: string) => usersApi.resetPassword(id, password));
