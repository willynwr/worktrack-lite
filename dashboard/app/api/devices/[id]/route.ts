import { NextResponse } from 'next/server';
import { getAdminToken } from '@/lib/session';

const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5031';

// GET /api/devices/[id] — proxy same-origin untuk polling Device Detail
// (status online/offline, app aktif, screenshot terakhir) dari Client Component.
export async function GET(_request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const token = await getAdminToken();
  if (!token) return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });

  const res = await fetch(`${API_BASE}/api/v1/dashboard/devices/${id}`, {
    headers: { Authorization: `Bearer ${token}` },
    cache: 'no-store',
  });

  const data = await res.json().catch(() => ({}));
  return NextResponse.json(data, { status: res.status });
}
