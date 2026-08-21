import { getAdminToken } from '@/lib/session';
import type { Device, DailyStats } from '@/lib/api';

// API_BASE dibaca di server (Next.js server-to-server ke .NET API) — lihat
// caveat Next.js: Server Component sebaiknya fetch langsung ke sumber data,
// bukan lewat Route Handler lokal (extra HTTP hop).
const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5031';

async function serverApiFetch<T>(path: string): Promise<T> {
  const token = await getAdminToken();

  const res = await fetch(`${API_BASE}${path}`, {
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    cache: 'no-store',
  });

  if (!res.ok) throw new Error(`API ${res.status}: ${path}`);
  return res.json();
}

export const api = {
  devices: () => serverApiFetch<Device[]>('/api/v1/dashboard/devices'),
  device:  (id: string) => serverApiFetch<Device>(`/api/v1/dashboard/devices/${id}`),
  stats:   (id: string, date: string) => serverApiFetch<DailyStats>(`/api/v1/dashboard/devices/${id}/stats?date=${date}`),
};
