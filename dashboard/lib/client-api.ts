'use client';

import type { TimelineResponse, Device } from '@/lib/api';

// Dipanggil dari Client Component — lewat proxy route lokal (app/api/timeline/[id])
// karena browser tidak bisa membaca httpOnly cookie admin_token untuk disisipkan
// sebagai header Authorization ke origin API yang berbeda.
export async function fetchTimeline(deviceId: string, date: string): Promise<TimelineResponse> {
  const res = await fetch(`/api/timeline/${deviceId}?date=${date}`);
  if (!res.ok) throw new Error(`Timeline fetch failed: ${res.status}`);
  return res.json();
}

// Fetcher generik untuk SWR — dipakai polling status device (online/offline,
// app aktif) supaya dashboard ter-update berkala tanpa reload manual.
export async function swrFetcher<T>(path: string): Promise<T> {
  const res = await fetch(path);
  if (!res.ok) throw new Error(`Fetch failed: ${res.status}`);
  return res.json();
}

export const DEVICES_POLL_INTERVAL_MS = 5000;

// Device Detail (screenshot + app aktif) — disamakan dengan siklus report agent (60s),
// tidak perlu lebih cepat karena data barunya memang baru ada tiap 1 menit.
export const DEVICE_DETAIL_POLL_INTERVAL_MS = 60000;

export type { Device };
