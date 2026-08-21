import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';
import { ADMIN_COOKIE } from '@/lib/session';

// Optimistic check saja (baca cookie, tidak validasi signature JWT) — cukup untuk
// redirect UX; otorisasi sebenarnya tetap divalidasi oleh .NET API di setiap request.
export function proxy(request: NextRequest) {
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
