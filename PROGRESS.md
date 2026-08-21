# WorkTrack Lite — Progress Report

Berikut adalah ringkasan status implementasi berdasarkan dokumen arsitektur `WorkTrack-Lite-Architecture.md`.

## 🟢 Selesai (Completed)

### Phase 1: Fondasi Agent + Registrasi
- [x] Scaffold .NET 8 (Core, Service, API).
- [x] EF Core + MySQL setup.
- [x] Registrasi device via `machine_key`.
- [x] Penyimpanan token aman di Agent menggunakan Windows DPAPI.
- [x] Endpoint `POST /api/v1/devices/register` dan `POST /api/v1/devices/heartbeat`.
- [x] Windows Service Worker untuk lifecycle agent.

### Phase 2: Active App + Idle + Uptime
- [x] Deteksi Foreground App (`GetForegroundWindow`).
- [x] Deteksi Idle Time (`GetLastInputInfo`).
- [x] Deteksi PC Uptime (`GetTickCount64`).
- [x] Upload activity report berkala (setiap 60 detik).
- [x] Penanganan idempotency (`client_uuid`).

### Phase 3: Screenshot Capture
- [x] Mekanisme capture layar (GDI `CopyFromScreen` fallback).
- [x] Penyimpanan gambar format JPEG.
- [x] Endpoint `POST /api/v1/screenshots` (multipart/form-data).
- [x] Abstraksi `IScreenshotStore` (Local File System).

### Phase 4: Offline Queue & Retry
- [x] Queue lokal di agent berbasis SQLite embedded.
- [x] Mekanisme enqueue otomatis bila upload (report/screenshot) gagal.
- [x] Flush otomatis saat agent kembali online (maksimal retry limit).

### Phase 5: Web Dashboard
- [x] Scaffold Next.js + TypeScript.
- [x] Endpoint khusus dashboard (`GET /dashboard/devices`, `GET /timeline`, `GET /stats`, `PATCH`).
- [x] Halaman `Overview` (Status real-time, auto-refresh tiap 5 detik via SWR).
- [x] Halaman `Devices` (Daftar semua PC, auto-refresh tiap 5 detik).
- [x] Halaman `Device Detail` (Info, statistik harian, auto-refresh tiap 60 detik untuk status/app aktif/screenshot terakhir).
- [x] ~~Timeline interaktif~~ **dihapus** — screenshot sekarang cuma disimpan 1 (terbaru) per device (lihat catatan storage di bawah), jadi riwayat per-menit tidak ada lagi untuk ditampilkan.

### Phase 6: Hardening (Sebagian)
- [x] Rate Limiting (Register: 5/min, Login: 10/min).
- [x] Admin Auth (Entity `AdminUser`, `JwtService`, Endpoint Login & Seed).
- [x] Filter Otorisasi JWT (`AdminJwtFilter`) untuk seluruh rute dashboard.
- [x] Retention Policy (`RetentionService` hapus data > 30 hari).
- [x] Migration untuk tabel `AdminUsers` & `AuditLogs` sudah dibuat.

---

### Phase 6: Hardening (lanjutan)
- [x] `AuditLogService` (`Services/AuditLogService.cs`) untuk mencatat aksi admin ke tabel `AuditLogs`.
- [x] Audit log terpasang di `PATCH /api/v1/dashboard/devices/{id}` (action `enabled_device`/`disabled_device`, mencatat admin, target device, dan IP).

### Phase 5/6: Dashboard Auth — perbaikan gap kritis
- [x] **Bug kritis kedua ditemukan & diperbaiki**: dashboard Next.js (Phase 5, sebelumnya ditandai "Selesai") ternyata **tidak pernah punya mekanisme login sama sekali** — tidak ada halaman `/login`, tidak ada penyimpanan token, dan `lib/api.ts` memanggil `/api/v1/dashboard/*` tanpa header/cookie apa pun. Setelah `AdminJwtFilter` ditambahkan di Phase 6, seluruh dashboard otomatis selalu 401. Selain itu, `<img>` yang menampilkan screenshot mengarah langsung ke API (`/api/v1/screenshots/file/{id}`) yang memakai device-token auth — tidak mungkin diautentikasi dari browser dashboard sama sekali.
- [x] Fix: dibangun mekanisme session penuh dengan pola **Backend-for-Frontend**:
  - `app/login/page.tsx` — form login, POST ke `app/api/session/route.ts` (Route Handler baru) yang meneruskan kredensial ke `.NET /api/v1/admin/login` lalu menyimpan JWT sebagai **httpOnly cookie** (`admin_token`) di domain dashboard sendiri (bukan domain API — cross-origin cookie tidak akan terkirim otomatis).
  - `lib/server-api.ts` (baru, Server Component) — fetch langsung ke .NET API dengan Authorization header dari cookie (`lib/session.ts`), sesuai rekomendasi Next.js untuk fetch data di Server Component langsung dari sumbernya (bukan lewat Route Handler lokal).
  - `app/api/timeline/[id]/route.ts` dan `app/api/screenshot/[id]/route.ts` (Route Handler baru) — proxy same-origin untuk kebutuhan Client Component (`TimelineClient`) dan `<img src>`, karena browser tidak bisa membaca httpOnly cookie untuk disisipkan sebagai header ke origin API yang berbeda.
  - `proxy.ts` (baru, root dashboard — nama file `middleware.ts` sudah deprecated di Next.js 16 ini, diganti `proxy.ts`) — redirect ke `/login` bila cookie admin tidak ada.
  - `app/_components/AppShell.tsx` + `LogoutButton.tsx` — sembunyikan sidebar di halaman login, tombol logout menghapus cookie via `DELETE /api/session`.
  - `ScreenshotEndpoints.ServeScreenshot` (.NET) diperluas: menerima admin JWT (header atau cookie) selain device token, dan mencatat audit log `viewed_screenshot` saat diakses admin — sekaligus menuntaskan item "audit log opsional saat admin melihat screenshot".
- [ ] **Belum diverifikasi dengan `npm run build`/`npm run dev`** — sesi ini diblokir safety classifier untuk menjalankan perintah di direktori dashboard (tidak terkait isi perubahan). Sudah direview manual (import, path alias `@/*`, tidak ada dependency baru yang butuh install — `server-only` sengaja tidak dipakai karena belum ter-install). **Wajib dijalankan `npm run build` dan uji login manual di browser sebelum dianggap selesai.**

## 🟡 Sedang Berjalan / Tersisa (Pending)

### 1. Installer (Phase 6)
- [x] Script Inno Setup dibuat (`installer/worktrack.iss`): install ke `Program Files\WorkTrack`, registrasi Windows Service (`sc create` binPath `WorkTrack.Service.exe`, start=auto), copy `WorkTrack.SessionAgent.exe` sebagai dependency di direktori sama, halaman wizard untuk input Server URL (ditulis ke `Environment` registry key service via `Agent__ServerUrl`), serta uninstall yang stop+delete service.
- [ ] Belum di-compile/diuji (butuh Inno Setup Compiler `iscc` di Windows — tidak tersedia di lingkungan dev ini/macOS). Build output publish (`dotnet publish -r win-x64`) untuk kedua project juga belum dijalankan — lihat komentar di header `.iss` untuk perintahnya.

### 2. Dokumentasi (Phase 6)
- [x] `docs/privacy-notice.md` — data yang dikumpulkan/tidak dikumpulkan, tujuan, retensi, akses, cara disable device, transparansi, kepatuhan.
- [x] `docs/admin-guide.md` — deploy server (migration, JWT secret, seed admin), deploy dashboard, build installer (dotnet publish + iscc), penggunaan dashboard, audit log, retention, uninstall.

### 3. Final Verification
- [x] Migration terakhir (`AddAdminAndAuditLog`) diterapkan ke MySQL lokal (`dotnet ef database update`) — semua tabel (`Devices`, `ActivityReports`, `Screenshots`, `AdminUsers`, `AuditLogs`) berhasil dibuat.
- [x] Test end-to-end server (tanpa Windows agent, karena environment dev ini macOS): seed admin → login → dapat JWT → register device (`POST /devices/register`) → `GET /dashboard/devices` → `PATCH /dashboard/devices/{id}` disable/enable → verifikasi audit log tercatat di tabel `AuditLogs` → verifikasi device nonaktif ditolak (401) saat heartbeat. Semua alur berjalan benar.
- [x] **Bug kritis ditemukan & diperbaiki**: `JwtService.ValidateToken` (`Auth/JwtService.cs`) selalu mengembalikan `null` untuk token admin yang valid, karena `JwtSecurityTokenHandler` secara default me-remap claim `sub` → URI claim lama (`.../nameidentifier`) saat validasi, sehingga `FindFirstValue(JwtRegisteredClaimNames.Sub)` tidak pernah menemukan claim tersebut. Akibatnya **seluruh dashboard admin gagal login/otentikasi sejak awal**, meski proses generate token benar. Fix: set `MapInboundClaims = false` pada `JwtSecurityTokenHandler` sebelum `ValidateToken`. Sudah diverifikasi ulang end-to-end setelah fix — login & seluruh endpoint dashboard kini berfungsi.
- [x] Test end-to-end dengan agent Windows sungguhan (PC-002) berhasil: registrasi via `machine_key`, heartbeat, activity report (foreground app + idle), dan upload screenshot semua terverifikasi masuk ke server &amp; tampil di dashboard.
- [ ] Offline-sync (SQLite queue) dengan agent Windows sungguhan belum dites eksplisit (memutus network lalu menyambung lagi) — logic-nya sudah ada (`LocalQueue.cs`), tapi belum diverifikasi langsung.

> Catatan lingkungan: verifikasi di atas dijalankan dengan MySQL lokal (Homebrew) sebagai pengganti server MySQL production untuk keperluan testing.

### Phase 5/6: Perbaikan lanjutan setelah testing end-to-end
- [x] **Bug ditemukan & diperbaiki**: dashboard Overview/Devices/Device Detail tidak auto-refresh — status online/offline & app aktif nyangkut stale sampai reload manual, karena Server Component cuma fetch sekali saat page load. Fix: polling via SWR — Overview/Devices tiap 5 detik (`app/_components/OverviewClient.tsx`, `DevicesClient.tsx`, proxy `app/api/devices/route.ts`), Device Detail tiap 60 detik selaras siklus report agent (`DeviceLiveCard.tsx`, proxy `app/api/devices/[id]/route.ts`).
- [x] **Bug reliability ditemukan & diperbaiki**: `SessionLauncher.LaunchInUserSession()` di `ServiceWorker.cs` cuma dipanggil sekali saat service start — kalau service di-set auto-start saat boot (start sebelum ada sesi user login), SessionAgent gagal diluncurkan dan **tidak pernah dicoba lagi** selama service hidup. Fix: `ServiceWorker` sekarang cek tiap siklus heartbeat (`SessionLauncher.IsSessionAgentRunning()`) dan relaunch otomatis bila SessionAgent belum/tidak jalan (self-healing).
- [x] **Perubahan retensi screenshot** (permintaan user): storage sebelumnya menyimpan semua screenshot sampai retention job 30 hari jalan → menumpuk cepat. Sekarang tiap upload baru langsung menghapus screenshot lama untuk device+monitor yang sama (DB row + file), jadi cuma 1 screenshot terbaru per device yang tersimpan (`ScreenshotEndpoints.UploadScreenshot`). Konsekuensi: fitur **Timeline** (riwayat screenshot per menit) jadi tidak berguna dan **dihapus** dari dashboard beserta proxy route & kode client-nya (endpoint `GET /dashboard/devices/{id}/timeline` di server tetap ada, tidak dipakai dashboard lagi).
- [x] Repo di-push ke GitHub: https://github.com/willynwr/worktrack-lite (public) — dipakai untuk `git clone` di PC Windows saat testing agent.
