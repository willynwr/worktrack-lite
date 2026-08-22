import { cookies } from 'next/headers';
import { ADMIN_COOKIE } from '@/lib/cookie-name';

export { ADMIN_COOKIE };

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
