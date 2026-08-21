import { NextResponse } from 'next/server';
import { setAdminToken, clearAdminToken } from '@/lib/session';

const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5031';

// POST /api/session — login: forward credentials ke .NET API, simpan JWT
// sebagai httpOnly cookie di domain dashboard (bukan domain API).
export async function POST(request: Request) {
  const body = await request.json().catch(() => null);
  if (!body?.username || !body?.password) {
    return NextResponse.json({ error: 'Username dan password wajib diisi.' }, { status: 400 });
  }

  let res: Response;
  try {
    res = await fetch(`${API_BASE}/api/v1/admin/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username: body.username, password: body.password }),
    });
  } catch {
    return NextResponse.json(
      { error: `Tidak dapat terhubung ke API server (${API_BASE}). Pastikan server .NET sedang berjalan.` },
      { status: 502 },
    );
  }

  const data = await res.json().catch(() => ({}));
  if (!res.ok) {
    return NextResponse.json({ error: data.error ?? 'Login gagal.' }, { status: res.status });
  }

  const expiresInHours = typeof data.expires_in_hours === 'number' ? data.expires_in_hours : 8;
  await setAdminToken(data.token, expiresInHours * 3600);

  return NextResponse.json({ ok: true });
}

// DELETE /api/session — logout: hapus cookie.
export async function DELETE() {
  await clearAdminToken();
  return NextResponse.json({ ok: true });
}
