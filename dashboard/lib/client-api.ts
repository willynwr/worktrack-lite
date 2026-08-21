'use client';

import type { TimelineResponse } from '@/lib/api';

// Dipanggil dari Client Component — lewat proxy route lokal (app/api/timeline/[id])
// karena browser tidak bisa membaca httpOnly cookie admin_token untuk disisipkan
// sebagai header Authorization ke origin API yang berbeda.
export async function fetchTimeline(deviceId: string, date: string): Promise<TimelineResponse> {
  const res = await fetch(`/api/timeline/${deviceId}?date=${date}`);
  if (!res.ok) throw new Error(`Timeline fetch failed: ${res.status}`);
  return res.json();
}
