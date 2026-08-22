import { cookies } from 'next/headers';
import { redirect } from 'next/navigation';

export const ADMIN_COOKIE = 'admin_token';

export async function getAdminToken(): Promise<string | null> {
  const store = await cookies();
  return store.get(ADMIN_COOKIE)?.value ?? null;
}

export async function setAdminToken(token: string, maxAgeSeconds: number) {
  const store = await cookies();
  store.set(ADMIN_COOKIE, token, {
    httpOnly: true,
    sameSite: 'lax',
    secure: process.env.NODE_ENV === 'production',
    path: '/',
    maxAge: maxAgeSeconds,
  });
}

export async function clearAdminToken() {
  const store = await cookies();
  store.delete(ADMIN_COOKIE);
}

// Dipanggil di awal tiap Server Component halaman yang butuh login — dulu ini ditangani
// Edge Middleware, tapi builder Vercel untuk versi Next.js ini gagal invoke Edge Middleware
// sama sekali (build gagal / crash saat dijalankan). Guard per-halaman ini jalan di runtime
// Node.js biasa yang sudah terbukti stabil.
export async function requireAdmin(): Promise<string> {
  const token = await getAdminToken();
  if (!token) redirect('/login');
  return token;
}
