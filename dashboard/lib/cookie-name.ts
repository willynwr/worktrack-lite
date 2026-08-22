// Nama cookie sesi admin — file terpisah tanpa dependency apa pun (khususnya bukan
// `next/headers`), supaya bisa diimpor dengan aman dari middleware.ts yang jalan di
// Edge Runtime (mengimpor lib/session.ts di sana bikin Vercel gagal deploy: "referencing
// unsupported modules", karena next/headers tidak didukung penuh di Edge Runtime).
export const ADMIN_COOKIE = 'admin_token';
