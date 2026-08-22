import { NextResponse } from 'next/server';
import { getAdminToken } from '@/lib/session';

const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5031';

// GET /api/devices/[id]/screenshots/download?from=&to= — proxy download ZIP dari .NET API,
// menyisipkan admin token. Browser trigger via navigasi biasa (bukan fetch), jadi respons
// di-stream apa adanya termasuk header Content-Disposition supaya browser save dialog jalan.
export async function GET(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const token = await getAdminToken();
  if (!token) return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });

  const { searchParams } = new URL(request.url);
  const qs = searchParams.toString();

  const res = await fetch(`${API_BASE}/api/v1/dashboard/devices/${id}/screenshots/download${qs ? `?${qs}` : ''}`, {
    headers: { Authorization: `Bearer ${token}` },
    cache: 'no-store',
  });

  if (!res.ok || !res.body) {
    const data = await res.json().catch(() => ({ error: 'Download gagal.' }));
    return NextResponse.json(data, { status: res.status || 500 });
  }

  return new NextResponse(res.body, {
    status: 200,
    headers: {
      'Content-Type': res.headers.get('content-type') ?? 'application/zip',
      'Content-Disposition': res.headers.get('content-disposition') ?? 'attachment',
    },
  });
}
