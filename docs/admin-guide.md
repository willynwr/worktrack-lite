# Admin Guide — WorkTrack Lite

Panduan untuk administrator IT: deploy server, build installer agent, dan penggunaan dashboard.

## 1. Prasyarat

- .NET 8/10 SDK (server & agent memakai `net10.0`).
- MySQL 8.x (server dan port dapat disesuaikan di connection string).
- Node.js 20+ (untuk dashboard Next.js).
- Windows 10/11 x64 di tiap PC target (agent hanya berjalan di Windows).
- Inno Setup Compiler (`iscc`) di mesin Windows untuk membangun installer.

## 2. Deploy server (API)

1. Konfigurasi `server/WorkTrack.Api/appsettings.json` (atau `appsettings.Production.json` / environment variables):

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "server=<host>;port=3306;database=worktrack;user=<user>;password=<pass>"
     },
     "ScreenshotStore": { "BasePath": "screenshots" },
     "Admin": {
       "JwtSecret": "<ganti dengan string acak minimal 32 karakter>",
       "JwtExpiryHours": 8,
       "RetentionDays": 30
     }
   }
   ```

   > **Wajib**: ganti `Admin:JwtSecret` di production — nilai default di repo hanya placeholder.

2. Apply migration EF Core ke MySQL:

   ```bash
   cd server/WorkTrack.Api
   dotnet ef database update
   ```

   (atau cukup jalankan `dotnet run` — API akan connect ke DB sesuai connection string; migration tetap perlu diterapkan manual dengan `dotnet ef database update` sebelum start pertama kali.)

3. Jalankan server:

   ```bash
   dotnet run --project server/WorkTrack.Api
   ```

4. Buat admin pertama (hanya bisa dipanggil sekali, ditolak bila sudah ada admin):

   ```bash
   curl -X POST https://<server>/api/v1/admin/seed \
     -H "Content-Type: application/json" \
     -d '{"username":"admin","password":"<password-kuat>"}'
   ```

5. Login untuk mendapatkan JWT:

   ```bash
   curl -X POST https://<server>/api/v1/admin/login \
     -H "Content-Type: application/json" \
     -d '{"username":"admin","password":"<password>"}'
   ```

## 3. Deploy dashboard (Next.js)

```bash
cd dashboard
npm install
```

Set `NEXT_PUBLIC_API_URL` di `.env.local` (atau environment variable saat deploy) mengarah ke URL server API, contoh:

```
NEXT_PUBLIC_API_URL=https://worktrack-api.perusahaan.local
```

Jalankan untuk development:

```bash
npm run dev
```

Untuk production:

```bash
npm run build
npm run start
```

Login dashboard menggunakan akun admin yang dibuat pada langkah 2.4.

## 4. Build installer agent (Windows)

Installer dibangun dengan Inno Setup (`installer/worktrack.iss`). Jalankan di mesin **Windows** yang memiliki .NET SDK dan Inno Setup Compiler terpasang:

```powershell
cd agent
dotnet publish WorkTrack.Service\WorkTrack.Service.csproj `
  -c Release -r win-x64 --self-contained false -o ..\installer\publish\service

dotnet publish WorkTrack.SessionAgent\WorkTrack.SessionAgent.csproj `
  -c Release -r win-x64 --self-contained false -o ..\installer\publish\sessionagent

cd ..\installer
iscc worktrack.iss
```

Hasil installer (`WorkTrackAgentSetup-<versi>.exe`) akan berada di `installer/output/`.

### Yang dilakukan installer

- Memasang `WorkTrack.Service.exe` (host Windows Service) dan `WorkTrack.SessionAgent.exe` (proses interaktif, diluncurkan oleh Service via `CreateProcessAsUser` — lihat `agent/WorkTrack.Service/SessionLauncher.cs`) ke `Program Files\WorkTrack`.
- Menanyakan **Server URL** saat instalasi, disimpan sebagai environment variable `Agent__ServerUrl` pada key registry service (`HKLM\SYSTEM\CurrentControlSet\Services\WorkTrackAgent\Environment`), dibaca otomatis oleh konfigurasi .NET.
- Mendaftarkan Windows Service (`sc create`, `start= auto`) sehingga agent berjalan otomatis sejak boot sebagai Local System.
- Uninstall (via Control Panel → Programs) menghentikan dan menghapus service serta seluruh file yang diinstal.

Registrasi device (`machine_key` → `device_id` + token) terjadi otomatis saat agent pertama kali berjalan — lihat `agent/WorkTrack.Service/ServiceWorker.cs`.

## 5. Menggunakan dashboard

- **Overview**: status online/offline seluruh device, aplikasi aktif terakhir.
- **Devices**: daftar semua PC terdaftar.
- **Device Detail**: info device, statistik harian (waktu aktif/idle, top apps), timeline interaktif dengan screenshot per menit.
- **Disable/Enable device**: dari halaman Device Detail, admin dapat menonaktifkan device (mis. saat PC dipindahtangankan atau karyawan resign). Device nonaktif akan ditolak servernya saat mengirim heartbeat/laporan baru (HTTP 403), sampai diaktifkan kembali.

## 6. Audit log

Setiap aksi admin yang mengubah status device (enable/disable) dicatat ke tabel `AuditLogs` melalui `AuditLogService` (`server/WorkTrack.Api/Services/AuditLogService.cs`), berisi:

- `AdminUsername` — admin yang melakukan aksi.
- `Action` — `enabled_device` / `disabled_device`.
- `Target` — device ID yang terdampak.
- `Timestamp`, `IpAddress`.

Untuk menginspeksi log, query langsung ke tabel `AuditLogs` di database (belum ada halaman dashboard khusus untuk menampilkannya).

## 7. Retention & storage

- Screenshot disimpan di filesystem lokal server (`ScreenshotStore:BasePath`, default `screenshots/` relatif terhadap working directory API).
- `RetentionService` berjalan sebagai background service, membersihkan screenshot yang lebih tua dari `Admin:RetentionDays` (default 30 hari) setiap 24 jam, termasuk saat startup.
- Pastikan disk server memiliki kapasitas cukup untuk menampung screenshot dari seluruh device selama periode retensi (perkirakan: jumlah device × screenshot/hari × ukuran rata-rata JPEG).

## 8. Uninstall agent

Uninstall melalui **Control Panel → Programs and Features → WorkTrack Agent → Uninstall**, atau:

```powershell
"C:\Program Files\WorkTrack\unins000.exe"
```

Ini akan menghentikan dan menghapus Windows Service beserta seluruh file yang diinstal. Data yang sudah terkirim ke server (di database dan screenshot store) **tidak** ikut terhapus — hapus manual dari dashboard/database bila diperlukan.

## 9. Kepatuhan

Sebelum deploy ke PC karyawan, baca dan patuhi [privacy-notice.md](privacy-notice.md) — mencakup data apa yang dikumpulkan, retensi, dan kewajiban pemberitahuan kepada karyawan sesuai kebijakan perusahaan dan hukum yang berlaku.
