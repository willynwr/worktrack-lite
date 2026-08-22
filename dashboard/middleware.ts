import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';
import { ADMIN_COOKIE } from '@/lib/cookie-name';

// Optimistic check saja (baca cookie, tidak validasi signature JWT) — cukup untuk
// redirect UX; otorisasi sebenarnya tetap divalidasi oleh .NET API di setiap request.
//
// Nama file/fungsi sengaja pakai konvensi lama `middleware.ts` (bukan `proxy.ts` yang
// jadi nama baru di Next.js 16) karena saat ditulis, builder Vercel belum mengenali
// `proxy.ts` — deploy sukses tapi semua route 404 di edge Vercel. `middleware.ts` masih
// didukung penuh (cuma deprecated), fungsinya identik.
export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const hasToken = request.cookies.has(ADMIN_COOKIE);

  if (pathname === '/login') {
    if (hasToken) return NextResponse.redirect(new URL('/', request.url));
    return NextResponse.next();
  }

  if (!hasToken) {
    return NextResponse.redirect(new URL('/login', request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ['/((?!api|_next/static|_next/image|favicon.ico).*)'],
};
