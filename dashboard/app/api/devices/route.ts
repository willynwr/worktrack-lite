import { NextResponse } from 'next/server';
import { getAdminToken } from '@/lib/session';

const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5031';

// GET /api/devices — proxy same-origin untuk polling dari Client Component
// (SWR di Overview/Devices), supaya status online/offline & app aktif ter-update
// berkala tanpa reload manual.
export async function GET() {
  const token = await getAdminToken();
  if (!token) return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });

  const res = await fetch(`${API_BASE}/api/v1/dashboard/devices`, {
    headers: { Authorization: `Bearer ${token}` },
    cache: 'no-store',
  });

  const data = await res.json().catch(() => ({}));
  return NextResponse.json(data, { status: res.status });
}
