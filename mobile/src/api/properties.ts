import { apiRequest } from './client';
import type { PagedResult } from './types';

export type PropertyStatus = 'Active' | 'Archived';
export type RoomStatus = 'Active' | 'Unavailable' | 'Archived';
export type BedStatus = 'Available' | 'Reserved' | 'Unavailable' | 'Archived';
export type BedOccupancy = 'Vacant' | 'Occupied' | 'Reserved' | 'Unavailable';
export type PropertySort = 'Name' | 'City' | 'Newest';

export interface OccupancySummary {
  totalBeds: number;
  occupied: number;
  vacant: number;
  reserved: number;
  unavailable: number;
}

export interface Property {
  id: string;
  name: string;
  address: string;
  city: string;
  state: string | null;
  postalCode: string | null;
  contactPhone: string | null;
  status: PropertyStatus;
  roomCount: number;
  occupancy: OccupancySummary;
  createdAt: string;
  updatedAt: string;
}

export interface Bed {
  id: string;
  roomId: string;
  label: string;
  status: BedStatus;
  occupancy: BedOccupancy;
  /** Rupees as a JSON number; never do arithmetic on it in the app. */
  defaultMonthlyRent: number | null;
}

export interface Room {
  id: string;
  propertyId: string;
  roomNumber: string;
  roomType: string | null;
  capacity: number;
  status: RoomStatus;
  beds: Bed[];
  occupancy: OccupancySummary;
}

export interface PropertyInput {
  name: string;
  address: string;
  city: string;
  state?: string;
  postalCode?: string;
  contactPhone?: string;
}

export interface CreateRoomInput {
  roomNumber: string;
  roomType?: string;
  capacity: number;
  createBeds: boolean;
  defaultMonthlyRent?: number;
}

export interface UpdateRoomInput {
  roomNumber: string;
  roomType?: string;
  capacity: number;
  status: 'Active' | 'Unavailable';
}

export interface BedInput {
  label: string;
  status: 'Available' | 'Reserved' | 'Unavailable';
  defaultMonthlyRent?: number;
}

const id = encodeURIComponent;

export const propertiesApi = {
  list: (
    params: { page: number; pageSize: number; search?: string; includeArchived?: boolean; sort?: PropertySort },
    signal?: AbortSignal,
  ) => {
    const query = new URLSearchParams({ page: String(params.page), pageSize: String(params.pageSize) });
    if (params.search?.trim()) query.set('search', params.search.trim());
    if (params.includeArchived) query.set('includeArchived', 'true');
    if (params.sort) query.set('sort', params.sort);
    return apiRequest<PagedResult<Property>>(`/api/v1/properties?${query.toString()}`, { signal });
  },
  get: (propertyId: string, signal?: AbortSignal) => apiRequest<Property>(`/api/v1/properties/${id(propertyId)}`, { signal }),
  create: (input: PropertyInput) => apiRequest<Property>('/api/v1/properties', { method: 'POST', body: input }),
  update: (propertyId: string, input: PropertyInput) =>
    apiRequest<Property>(`/api/v1/properties/${id(propertyId)}`, { method: 'PUT', body: input }),
  archive: (propertyId: string) => apiRequest<void>(`/api/v1/properties/${id(propertyId)}`, { method: 'DELETE' }),
  restore: (propertyId: string) => apiRequest<Property>(`/api/v1/properties/${id(propertyId)}/restore`, { method: 'POST' }),

  rooms: (propertyId: string, signal?: AbortSignal) => apiRequest<Room[]>(`/api/v1/properties/${id(propertyId)}/rooms`, { signal }),
  createRoom: (propertyId: string, input: CreateRoomInput) =>
    apiRequest<Room>(`/api/v1/properties/${id(propertyId)}/rooms`, { method: 'POST', body: input }),
};

export const roomsApi = {
  get: (roomId: string, signal?: AbortSignal) => apiRequest<Room>(`/api/v1/rooms/${id(roomId)}`, { signal }),
  update: (roomId: string, input: UpdateRoomInput) => apiRequest<Room>(`/api/v1/rooms/${id(roomId)}`, { method: 'PUT', body: input }),
  archive: (roomId: string) => apiRequest<void>(`/api/v1/rooms/${id(roomId)}`, { method: 'DELETE' }),
  addBed: (roomId: string, input: { label?: string; defaultMonthlyRent?: number }) =>
    apiRequest<Bed>(`/api/v1/rooms/${id(roomId)}/beds`, { method: 'POST', body: input }),
};

export const bedsApi = {
  get: (bedId: string, signal?: AbortSignal) => apiRequest<Bed>(`/api/v1/beds/${id(bedId)}`, { signal }),
  update: (bedId: string, input: BedInput) => apiRequest<Bed>(`/api/v1/beds/${id(bedId)}`, { method: 'PUT', body: input }),
  archive: (bedId: string) => apiRequest<void>(`/api/v1/beds/${id(bedId)}`, { method: 'DELETE' }),
};
