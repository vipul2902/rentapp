import type { StaffPermission, UserProfile } from '@/api/types';

export const STAFF_PERMISSIONS: readonly { key: StaffPermission; label: string; description: string }[] = [
  { key: 'ViewProperties', label: 'View properties', description: 'See properties, rooms and beds' },
  { key: 'ViewTenants', label: 'View tenants', description: 'See tenant details and balances' },
  { key: 'RecordPayments', label: 'Record payments', description: 'Record cash, UPI and bank payments' },
  { key: 'GenerateReceipts', label: 'Generate receipts', description: 'Create and share receipts' },
  { key: 'SendReminders', label: 'Send reminders', description: 'Copy and share rent reminders' },
];

/** UI convenience only; the API enforces every permission on its side. */
export function can(user: UserProfile, permission: StaffPermission): boolean {
  return user.role === 'Owner' || user.permissions.includes(permission);
}
