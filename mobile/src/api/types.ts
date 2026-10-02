/** Wire types mirroring the API's DTOs (enums are serialized as strings). */

export type UserRole = 'Owner' | 'Staff';
export type UserStatus = 'Active' | 'Disabled';
export type StaffPermission = 'ViewProperties' | 'ViewTenants' | 'RecordPayments' | 'GenerateReceipts' | 'SendReminders';

export interface OrganizationSummary {
  id: string;
  name: string;
  timeZone: string;
}

export interface UserProfile {
  id: string;
  name: string;
  email: string;
  phone: string | null;
  role: UserRole;
  status: UserStatus;
  permissions: StaffPermission[];
  organization: OrganizationSummary;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: UserProfile;
}

export interface StaffMember {
  id: string;
  name: string;
  email: string;
  phone: string | null;
  role: UserRole;
  status: UserStatus;
  permissions: StaffPermission[];
  lastLoginAt: string | null;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
