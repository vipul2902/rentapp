import { useQuery } from '@tanstack/react-query';

import { getReadiness } from '@/api/health';

export const readinessQueryKey = ['health', 'ready'] as const;

export function useReadiness() {
  return useQuery({
    queryKey: readinessQueryKey,
    queryFn: ({ signal }) => getReadiness(signal),
    staleTime: 0,
  });
}
