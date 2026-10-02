import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { File, Paths } from 'expo-file-system';
import * as Sharing from 'expo-sharing';
import { useRef } from 'react';

import { authorizedDownload, newIdempotencyKey } from '@/api/client';
import { ApiClientError } from '@/api/errors';
import { paymentsApi, type RecordPaymentInput } from '@/api/payments';

import { rentKeys } from './useRent';
import { tenantKeys } from './useTenants';

export const paymentKeys = {
  all: ['payments'] as const,
  list: (tenantId?: string) => ['payments', 'list', tenantId ?? ''] as const,
  detail: (id: string) => ['payments', 'detail', id] as const,
  receipt: (id: string) => ['payments', 'receipt', id] as const,
};

/** A payment changes balances everywhere: dues, tenants, the dashboard. */
async function refreshAfterPayment(queryClient: ReturnType<typeof useQueryClient>) {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: paymentKeys.all }),
    queryClient.invalidateQueries({ queryKey: rentKeys.all }),
    queryClient.invalidateQueries({ queryKey: tenantKeys.all }),
  ]);
}

/**
 * Records a payment. One idempotency key is kept for the life of the form, so tapping Save again after a
 * timeout (when the first request may have reached the server) cannot record the payment twice.
 */
export function useRecordPayment() {
  const queryClient = useQueryClient();
  const key = useRef(newIdempotencyKey());
  return useMutation({
    mutationFn: (input: RecordPaymentInput) => paymentsApi.record(input, key.current),
    onSuccess: async (result) => {
      queryClient.setQueryData(paymentKeys.receipt(result.receipt.id), result.receipt);
      queryClient.setQueryData(paymentKeys.detail(result.payment.id), result.payment);
      // The next payment from this form is a new payment.
      key.current = newIdempotencyKey();
      await refreshAfterPayment(queryClient);
    },
  });
}

export const usePayment = (id: string) =>
  useQuery({ queryKey: paymentKeys.detail(id), queryFn: ({ signal }) => paymentsApi.get(id, signal) });

export const useReceipt = (id: string) =>
  useQuery({ queryKey: paymentKeys.receipt(id), queryFn: ({ signal }) => paymentsApi.receipt(id, signal) });

export function usePaymentHistory(tenantId: string, enabled = true) {
  return useInfiniteQuery({
    queryKey: paymentKeys.list(tenantId),
    queryFn: ({ pageParam, signal }) => paymentsApi.list({ page: pageParam, pageSize: 10, tenantId }, signal),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.page < last.totalPages ? last.page + 1 : undefined),
    enabled,
  });
}

export function useVoidPayment(paymentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (reason: string) => paymentsApi.void(paymentId, reason),
    onSuccess: () => refreshAfterPayment(queryClient),
  });
}

/** Downloads the receipt PDF (with the user's token) and opens the share sheet: WhatsApp, email, save to files. */
export function useShareReceipt() {
  return useMutation({
    mutationFn: async ({ receiptId, receiptNumber }: { receiptId: string; receiptNumber: string }) => {
      const { url, headers } = await authorizedDownload(paymentsApi.receiptPdfPath(receiptId));
      let file: File;
      try {
        file = await File.downloadFileAsync(url, new File(Paths.cache, `${receiptNumber}.pdf`), { headers, idempotent: true });
      } catch {
        throw new ApiClientError({ kind: 'network' });
      }
      if (!(await Sharing.isAvailableAsync())) {
        throw new Error('Sharing is not available on this device.');
      }
      await Sharing.shareAsync(file.uri, { mimeType: 'application/pdf', UTI: 'com.adobe.pdf', dialogTitle: `Receipt ${receiptNumber}` });
    },
  });
}
