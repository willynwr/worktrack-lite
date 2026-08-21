import { NextResponse } from 'next/server';
import { getAdminToken } from '@/lib/session';

const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5031';

// GET /api/timeline/[id]?date=YYYY-MM-DD — proxy ke .NET dashboard timeline
// endpoint, menyisipkan admin token dari httpOnly cookie (browser tidak bisa
// membacanya sendiri untuk memanggil origin API secara langsung).
export async function GET(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const token = await getAdminToken();
  if (!token) return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });

  const date = new URL(request.url).searchParams.get('date');
  const qs = date ? `?date=${encodeURIComponent(date)}` : '';

  const res = await fetch(`${API_BASE}/api/v1/dashboard/devices/${id}/timeline${qs}`, {
    headers: { Authorization: `Bearer ${token}` },
    cache: 'no-store',
  });

  const data = await res.json().catch(() => ({}));
  return NextResponse.json(data, { status: res.status });
}
