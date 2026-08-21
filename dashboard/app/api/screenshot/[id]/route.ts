import { NextResponse } from 'next/server';
import { getAdminToken } from '@/lib/session';

const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5031';

// GET /api/screenshot/[id] — proxy byte gambar dari .NET API, menyisipkan
// admin token. <img src="/api/screenshot/123"> same-origin terhadap dashboard,
// jadi tidak perlu CORS/credentials khusus di sisi browser.
export async function GET(_request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const token = await getAdminToken();
  if (!token) return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });

  const res = await fetch(`${API_BASE}/api/v1/screenshots/file/${id}`, {
    headers: { Authorization: `Bearer ${token}` },
    cache: 'no-store',
  });

  if (!res.ok || !res.body) {
    return NextResponse.json({ error: 'Screenshot not found' }, { status: res.status || 404 });
  }

  return new NextResponse(res.body, {
    status: 200,
    headers: {
      'Content-Type': res.headers.get('content-type') ?? 'image/jpeg',
      'Cache-Control': 'private, max-age=60',
    },
  });
}
