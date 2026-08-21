# WorkTrack Lite — Architecture (Final, Revisi)

Dokumen arsitektur baseline untuk **WorkTrack Lite**: sistem monitoring produktivitas ringan untuk **PC Windows milik perusahaan**. Dokumen ini sudah menyerap seluruh perbaikan dari review dan bersifat mandiri — dapat dipakai langsung sebagai acuan implementasi.

> **Prinsip pembatas (tidak dapat dinegosiasi).** Software ini hanya untuk PC milik perusahaan dan penggunaan kerja. "Headless" berarti **tanpa UI yang mengganggu**, bukan stealth/evasion. Seluruh larangan di [§3](#3-hal-yang-secara-eksplisit-dilarang) diperlakukan sebagai batas keras: tidak ada keylogger, keyboard hook, clipboard/webcam/mic, injection, anti-debug, AV/EDR bypass, penyembunyian dari Task Manager, atau penghindaran security review. Agent harus terlihat, dapat diaudit, dan dapat di-uninstall administrator.

---

## Daftar Isi

1. [Tujuan & Ruang Lingkup](#1-tujuan--ruang-lingkup)
2. [Data yang Boleh Dikumpulkan](#2-data-yang-boleh-dikumpulkan)
3. [Hal yang Secara Eksplisit Dilarang](#3-hal-yang-secara-eksplisit-dilarang)
4. [Gambaran Sistem](#4-gambaran-sistem)
5. [Windows Agent Architecture](#5-windows-agent-architecture)
6. [Automatic Registration](#6-automatic-registration)
7. [Reporting & Offline Queue](#7-reporting--offline-queue)
8. [Backend API](#8-backend-api)
9. [Database Schema](#9-database-schema)
10. [Web Dashboard](#10-web-dashboard)
11. [Statistics](#11-statistics)
12. [Screenshot Storage](#12-screenshot-storage)
13. [Security](#13-security)
14. [Deployment](#14-deployment)
15. [Installer](#15-installer)
16. [Privacy / Compliance](#16-privacy--compliance)
17. [Struktur Folder](#17-struktur-folder)
18. [Prioritas Implementasi (Phase 1–6)](#18-prioritas-implementasi-phase-16)
19. [Ringkasan Keputusan Kunci](#19-ringkasan-keputusan-kunci)

---

## 1. Tujuan & Ruang Lingkup

Sistem monitoring ringan dengan karakteristik:

- Windows Agent **tanpa UI interaktif** — tidak ada popup/jendela yang mengganggu kerja.
- Dashboard **sepenuhnya web-based** — tidak ada aplikasi desktop dashboard.
- Mendukung hingga **10 PC Windows** (MVP).
- Setelah agent diinstal & dikonfigurasi administrator, agent **otomatis** melakukan registration lalu mengirim heartbeat + report.
- Server mencatat info jaringan (local/private IP) sebagai **informasi device saja**.
- Setiap PC memiliki **`device_id` stabil**; IP **tidak** dipakai sebagai identitas utama karena bisa berubah.

**Skala target:** 1 server, 10 PC agent, 1 web dashboard. Semua keputusan teknis dioptimalkan untuk kesederhanaan pada skala ini, dengan titik ekstensi agar bisa tumbuh (Postgres, S3) tanpa refactor besar.

## 2. Data yang Boleh Dikumpulkan

Hanya data berikut, tidak lebih.

**Screenshot** — 1 setiap 60 detik; format JPG atau WebP dengan kompresi efisien; disertai timestamp, `device_id`, dan `monitor_index` bila multi-monitor.

**Active application** — hanya nama executable foreground (mis. `chrome.exe`, `AfterFX.exe`, `Blender.exe`). **Window title tidak disimpan.**

**Idle** — via `GetLastInputInfo`, disimpan sebagai `idle_seconds`. **Tanpa keyboard hook, tanpa menyimpan isi ketikan.**

**PC uptime** — via `GetTickCount64()` (uptime OS, bukan uptime proses), disimpan sebagai `uptime_seconds`.

**Device information** (saat registration, minimal): `device_id`, `hostname`, Windows version, agent version, local/private IP, `registered_at`, `last_seen_at`.

**Tidak dikumpulkan:** file pribadi, browser cookies, clipboard, password, webcam, microphone, isi komunikasi.

## 3. Hal yang Secara Eksplisit Dilarang

Tidak boleh diimplementasikan, dalam bentuk apa pun:

keylogger · keyboard hook · clipboard monitoring · password capture · browser cookie extraction · webcam · microphone recording · file-content scanning · chat-content capture · credential harvesting · process injection · DLL injection · anti-debugging · anti-analysis · AV/EDR bypass · obfuscation untuk menghindari security software · hiding from Task Manager · disabling antivirus · persistence non-standar (di luar mekanisme Windows Service resmi) · teknik apa pun untuk mengelabui atau menyembunyikan aktivitas monitoring dari security tools.

## 4. Gambaran Sistem

```text
                         ┌───────────────────────────────┐
                         │            SERVER              │
   PC-01 ─┐              │  ┌─────────────────────────┐  │
   PC-02 ─┤   HTTPS      │  │   WorkTrack.Api          │  │
   PC-03 ─┼───────────►  │  │   (ASP.NET Core, .NET 8) │  │
    ...   │  register    │  └──────────┬──────────────┘  │
   PC-10 ─┘  heartbeat   │             │                  │
              reports    │   ┌─────────▼───────┐          │
              screenshots│   │  MySQL (EF Core) │          │
                         │   └─────────────────┘          │
                         │   ┌─────────────────┐          │
                         │   │ Screenshot Store │          │
                         │   │ (local FS)       │          │
                         │   └─────────────────┘          │
                         └──────────────▲────────────────┘
                                        │ HTTPS (session admin)
                                ┌───────┴────────┐
                                │  Web Dashboard  │
                                │ (Next.js + TS)  │
                                └────────────────┘
```

## 5. Windows Agent Architecture

Tiga komponen, target **.NET 8**:

```text
WorkTrack.Core          # shared: models, config, DPAPI, HTTP client, MySQL queue
WorkTrack.Service       # Windows Service (lifecycle + spawn SessionAgent)
WorkTrack.SessionAgent  # jalan di interactive session (capture + report)
```

### WorkTrack.Service

Windows Service yang: start otomatis lewat **mekanisme Windows Service standar**; memastikan `SessionAgent` berjalan pada **interactive user session** (via `WTSQueryUserToken` + `CreateProcessAsUser`); **tidak** melakukan screen capture dari Session 0; **tanpa UI**; menangani lifecycle agent; dan mengelola heartbeat bila diperlukan.

Pemisahan Service ↔ SessionAgent bukan opsional: Windows Service berjalan di Session 0 yang non-interaktif dan tidak dapat menangkap layar pengguna. Screenshot **harus** diambil dari proses di session interaktif. Ini teknik resmi Windows, bukan stealth.

### WorkTrack.SessionAgent

Berjalan di interactive Windows session, menangani: **Screenshot**, **Foreground Application**, **Idle Detection**, **PC Uptime**, **Local Queue**, **Upload**.

API Windows resmi yang dipakai:

| Fungsi | API |
|---|---|
| Screenshot (utama) | `Windows.Graphics.Capture` |
| Screenshot (fallback Win10 lama) | `Direct3D11CaptureFramePool` / `BitBlt` |
| Foreground app | `GetForegroundWindow` → `GetWindowThreadProcessId` → nama exe |
| Idle | `GetLastInputInfo` |
| Uptime | `GetTickCount64` |

Catatan capture: `Windows.Graphics.Capture` dapat menampilkan yellow capture border pada Windows lama; sejak Windows 11 border dapat dinonaktifkan lewat API resmi. Fallback disiapkan **hanya untuk kompatibilitas**, bukan untuk menyembunyikan aktivitas.

## 6. Automatic Registration

Registration terjadi sekali (saat instalasi/konfigurasi). Identitas berbasis **machine_key**, bukan IP.

```text
Agent
  → machine_key = hash(HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid)   # stabil
  → hostname, windows_version, agent_version, local_ip
  → POST /api/v1/devices/register  (HTTPS)
  → Server: cek machine_key
        - baru      → buat device_id + device_token acak
        - sudah ada → kembalikan device_id existing (tidak duplikat)
  → Server simpan token_hash (bukan plaintext); kembalikan token plaintext SEKALI
  → Agent simpan token via Windows DPAPI (ProtectedData, scope LocalMachine)
  → Heartbeat dimulai
```

**Contoh response (201):**

```json
{
  "device_id": "PC-001",
  "device_token": "wt_live_9f2c...one-time...",
  "status": "registered",
  "server_time": "2026-08-21T11:30:00+07:00"
}
```

Perbaikan penting dari desain awal: kata "credential" kini konkret — **token acak per-device**, disimpan agent lewat **DPAPI** (bukan plaintext di config), dan server hanya menyimpan **hash**-nya.

## 7. Reporting & Offline Queue

Setiap 60 detik `SessionAgent` menyusun satu record dan mengunggahnya. **Screenshot dikirim terpisah dari report JSON** (bukan base64 inline) agar payload report tetap kecil dan efisien.

**Report JSON (kecil) → `POST /reports`:**

```json
{
  "device_id": "PC-001",
  "client_uuid": "7b1e...unik-per-record",
  "timestamp": "2026-08-21T11:31:00+07:00",
  "active_app": "AfterFX.exe",
  "idle_seconds": 0,
  "uptime_seconds": 45210
}
```

**Screenshot (biner) → `POST /screenshots`** sebagai `multipart/form-data`, direferensikan dengan `client_uuid` + `monitor_index` yang sama.

**Idempotency:** `client_uuid` adalah kunci idempotency. Server menegakkan `UNIQUE(device_id, client_uuid)` — upload ulang record yang sama tidak menghasilkan duplikat.

**Offline recovery:**

```text
upload gagal → simpan record + screenshot ke MySQL local queue
internet kembali → kirim ulang pending records (report + screenshot)
server idempotent by (device_id, client_uuid) → tanpa duplikat
2xx diterima → hapus dari queue
```

## 8. Backend API

Stack: **ASP.NET Core (.NET 8)**, Minimal API. Semua endpoint **HTTPS**. Device endpoint memakai `Authorization: Bearer <device_token>` kecuali `register`. Dashboard endpoint memakai session admin.

```text
POST  /api/v1/devices/register
  Auth  : none (enrollment)
  Body  : { machine_key, hostname, windows_version, agent_version, local_ip }
  201   : { device_id, device_token, status:"registered", server_time }
  200/409: machine_key sudah ada → kembalikan device_id existing (tanpa duplikat)

POST  /api/v1/devices/heartbeat
  Auth  : device token
  Body  : { device_id, local_ip?, uptime_seconds }
  200   : { status:"ok", server_time }          # update last_seen_at

POST  /api/v1/reports
  Auth  : device token
  Body  : { device_id, client_uuid, timestamp, active_app, idle_seconds, uptime_seconds }
  201   : { report_id, accepted:true }
  200   : { accepted:true, duplicate:true }      # client_uuid sudah ada (idempotent)

POST  /api/v1/screenshots
  Auth  : device token
  Type  : multipart/form-data
  Fields: device_id, client_uuid, timestamp, monitor_index, file (jpg|webp)
  201   : { screenshot_id, accepted:true }
  200   : { accepted:true, duplicate:true }

GET   /api/v1/devices                                   # daftar + status ringkas
GET   /api/v1/devices/{id}                              # detail device
GET   /api/v1/devices/{id}/timeline?date=YYYY-MM-DD     # activity_reports terurut
GET   /api/v1/devices/{id}/screenshots?date=YYYY-MM-DD  # metadata + URL
GET   /api/v1/devices/{id}/stats?date=YYYY-MM-DD        # statistik on-read
PATCH /api/v1/devices/{id}   { is_active:false }        # admin nonaktifkan device
```

Status **Online/Offline** dihitung dari `last_seen_at` (mis. Offline bila selisih > 3× interval heartbeat).

## 9. Database Schema

**MySQL server-side via EF Core** (satu engine, nol ops). Disembunyikan di balik EF Core agar migrasi ke PostgreSQL kelak trivial. Untuk 10 PC tidak diperlukan tabel agregat/materialized view — statistik dihitung on-read.

```sql
-- devices
CREATE TABLE devices (
    device_id       TEXT PRIMARY KEY,            -- server-generated, mis. "PC-001"
    hostname        TEXT NOT NULL,
    windows_version TEXT NOT NULL,
    agent_version   TEXT NOT NULL,
    local_ip        TEXT,                        -- informasi saja, bukan identitas
    machine_key     TEXT NOT NULL UNIQUE,        -- hash MachineGuid, anti-duplicate register
    token_hash      TEXT NOT NULL,               -- hash device token (bukan plaintext)
    registered_at   TEXT NOT NULL,
    last_seen_at    TEXT,
    is_active       INTEGER NOT NULL DEFAULT 1   -- admin bisa nonaktifkan device
);

-- activity_reports
CREATE TABLE activity_reports (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    device_id       TEXT NOT NULL REFERENCES devices(device_id),
    client_uuid     TEXT NOT NULL,               -- idempotency key
    timestamp       TEXT NOT NULL,
    active_app      TEXT,                         -- nama exe saja
    idle_seconds    INTEGER NOT NULL,
    uptime_seconds  INTEGER NOT NULL,
    created_at      TEXT NOT NULL,
    UNIQUE(device_id, client_uuid)               -- cegah duplicate upload
);
CREATE INDEX ix_reports_device_time ON activity_reports(device_id, timestamp);

-- screenshots
CREATE TABLE screenshots (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    device_id       TEXT NOT NULL REFERENCES devices(device_id),
    client_uuid     TEXT NOT NULL,
    timestamp       TEXT NOT NULL,
    monitor_index   INTEGER NOT NULL DEFAULT 0,
    storage_path    TEXT NOT NULL,               -- path/key di IScreenshotStore
    content_type    TEXT NOT NULL,               -- image/jpeg | image/webp
    size_bytes      INTEGER NOT NULL,
    created_at      TEXT NOT NULL,
    UNIQUE(device_id, client_uuid, monitor_index)
);
CREATE INDEX ix_shots_device_time ON screenshots(device_id, timestamp);

-- admin_users
CREATE TABLE admin_users (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    username        TEXT NOT NULL UNIQUE,
    password_hash   TEXT NOT NULL,               -- Argon2id / bcrypt
    created_at      TEXT NOT NULL,
    last_login_at   TEXT
);

-- audit_log (Phase 6)
CREATE TABLE audit_log (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    admin_id        INTEGER REFERENCES admin_users(id),
    action          TEXT NOT NULL,               -- mis. viewed_screenshot, disabled_device
    target          TEXT,
    timestamp       TEXT NOT NULL,
    ip              TEXT
);
```

## 10. Web Dashboard

Stack: **Next.js + TypeScript**. Sepenuhnya web-based, tanpa desktop dashboard.

**Halaman Devices** — tabel: PC Name, Device ID, IP, Online/Offline, Last Seen, Current App, Idle, Uptime.

**Halaman Device Detail** — Current App, Idle Time, PC Uptime, Last Screenshot, Screenshot Timeline, Daily Statistics.

**Screenshot Timeline** — screenshot ditampilkan berdasarkan timestamp:

```text
11:00  11:01  11:02  11:03  ...
```

Akses screenshot & device endpoint diproteksi otorisasi admin ([§13](#13-security)).

## 11. Statistics

Dihitung **on-read** dari `activity_reports` (tanpa tabel agregat, cukup untuk 10 PC): Total PC uptime, Total active time, Total idle time, Application usage duration, Screenshot count, Last seen.

**Tidak** membuat productivity score yang menghakimi atau menyimpulkan perilaku karyawan tanpa dasar data.

## 12. Screenshot Storage

MVP: **local filesystem** di balik abstraksi `IScreenshotStore`, dengan path terstruktur:

```text
screenshots/{device_id}/{YYYY-MM-DD}/{timestamp}_{monitor_index}.jpg
```

Abstraksi memungkinkan implementasi **S3-compatible** menyusul tanpa refactor. Prioritas awal: implementasi paling sederhana untuk development.

## 13. Security

Wajib: HTTPS · dashboard ter-autentikasi · device authentication (bearer token) · password hashing (Argon2id/bcrypt) · secure credential storage (DPAPI di agent, hash di server) · input validation · rate limiting dasar · audit log admin · retention policy · authorization pada endpoint screenshot/device.

Tidak boleh ada mekanisme security-evasion apa pun.

Penjadwalan: HTTPS, device auth, admin auth, password hashing, dan input validation dibangun sejak awal; rate limiting, audit log, dan retention policy diselesaikan di **Phase 6 (hardening)** agar tidak menghambat Phase 1 tanpa mengorbankan kewajibannya.

## 14. Deployment

```text
Server
 ├── WorkTrack.Api
 ├── MySQL database
 └── Screenshot storage (local FS)

PC-01 → Agent (Service + SessionAgent)
PC-02 → Agent
 ...
PC-10 → Agent
```

Deployment dibuat sederhana: satu server host API + DB + storage; tiap PC menjalankan agent lewat installer.

## 15. Installer

Installer Windows (Inno Setup / WiX) yang: membutuhkan **administrator permission**; menginstal `WorkTrack.Service` + `SessionAgent`; melakukan initial configuration; mendaftarkan device; memakai **mekanisme Windows Service standar**; **tidak** menampilkan UI agent saat monitoring berjalan; dapat di-uninstall administrator; dan **tidak** menyembunyikan service dari Task Manager atau security software.

## 16. Privacy / Compliance

Ditujukan **hanya** untuk PC milik perusahaan & penggunaan kerja. Wajib didokumentasikan (`docs/privacy-notice.md`): data apa yang dikumpulkan, tujuan pengumpulan, retention, siapa yang dapat melihat screenshot, cara administrator menonaktifkan device, dan pernyataan bahwa keylogging & credential capture tidak dilakukan.

Tidak boleh ada fitur untuk menghindari kewajiban hukum, consent/notice, kebijakan perusahaan, antivirus, EDR, atau security review.

## 17. Struktur Folder

```text
worktrack-lite/
├── agent/                              # Windows Agent (.NET 8)
│   ├── WorkTrack.Core/                 # shared: models, config, DPAPI, HTTP client, queue
│   │   ├── Models/
│   │   ├── Security/CredentialStore.cs # DPAPI wrapper
│   │   ├── Net/ApiClient.cs
│   │   └── Queue/MySqlQueue.cs
│   ├── WorkTrack.Service/              # Windows Service host
│   │   ├── Program.cs
│   │   ├── ServiceWorker.cs            # lifecycle + heartbeat
│   │   └── SessionLauncher.cs          # WTSQueryUserToken + CreateProcessAsUser
│   └── WorkTrack.SessionAgent/         # jalan di interactive session
│       ├── Program.cs
│       ├── Capture/ScreenCapturer.cs   # Windows.Graphics.Capture (+fallback)
│       ├── Foreground/AppMonitor.cs    # GetForegroundWindow -> exe name
│       ├── Idle/IdleMonitor.cs         # GetLastInputInfo
│       ├── Uptime/UptimeMonitor.cs     # GetTickCount64
│       └── Reporting/Reporter.cs       # kumpulkan record 60s, enqueue, upload
│
├── server/                             # Backend (ASP.NET Core, .NET 8)
│   └── WorkTrack.Api/
│       ├── Program.cs
│       ├── Endpoints/                  # minimal API groups
│       ├── Data/                       # EF Core DbContext + migrations
│       ├── Storage/IScreenshotStore.cs # Local + (nanti) S3
│       ├── Auth/                       # device token + admin auth
│       └── appsettings.json
│
├── dashboard/                          # Web dashboard (Next.js + TS)
│   ├── app/
│   │   ├── devices/
│   │   ├── devices/[id]/
│   │   └── login/
│   ├── lib/api.ts
│   └── package.json
│
├── installer/                          # Inno Setup / WiX
│   └── worktrack.iss
│
└── docs/
    ├── privacy-notice.md
    └── admin-guide.md
```

## 18. Prioritas Implementasi (Phase 1–6)

Setiap phase **harus dapat di-build dan dites** sebelum lanjut. Jangan membangun semua fitur sekaligus.

### Phase 1 — Fondasi Agent + Registrasi

Scope: `WorkTrack.Core`, `WorkTrack.Service`, `WorkTrack.Api` minimal (`register` + `heartbeat`), EF Core + MySQL, migrasi awal (`devices`).

Langkah:
1. Scaffold solution (.NET 8): Core, Service, Api.
2. Core: models (`DeviceInfo`, `RegisterRequest/Response`) + baca server URL dari config.
3. `CredentialStore` (DPAPI, scope `LocalMachine`).
4. `machine_key` dari `MachineGuid` (di-hash).
5. `ApiClient`: `RegisterAsync`, `HeartbeatAsync` + retry sederhana.
6. Api minimal: `POST /devices/register`, `POST /devices/heartbeat`.
7. Service (`BackgroundService`): start → cek token → register bila perlu → mulai heartbeat timer.
8. Uji manual: `sc create` / `New-Service`.

Kriteria selesai: service Running & auto-start; register pertama membuat row `devices` + token tersimpan via DPAPI (bukan plaintext); restart tidak register ulang; heartbeat memperbarui `last_seen_at`; register kedua dengan machine_key sama tidak menghasilkan duplikat.

### Phase 2 — Active App + Idle + Uptime

`AppMonitor` (`GetForegroundWindow` → exe), `IdleMonitor` (`GetLastInputInfo`), `UptimeMonitor` (`GetTickCount64`). Aktifkan spawn `SessionAgent` di interactive session via `WTSQueryUserToken`/`CreateProcessAsUser`. Endpoint `POST /reports` aktif. Uji: record muncul di `activity_reports`.

### Phase 3 — Screenshot 60 detik

`ScreenCapturer` (`Windows.Graphics.Capture` + fallback), JPG/WebP, `monitor_index`. Endpoint `POST /screenshots` (multipart) + `IScreenshotStore` (local FS). Uji: screenshot tersimpan & terhubung ke device.

### Phase 4 — Offline Queue + Retry + Idempotency

`MySqlQueue` lokal; kirim ulang saat online; server idempotent by `(device_id, client_uuid)`. Uji: matikan koneksi → record tertahan → nyalakan → terkirim tanpa duplikat.

### Phase 5 — Web Dashboard

Next.js + TS: login, Devices, Device Detail, Screenshot Timeline, Daily Statistics; endpoint `GET devices/timeline/screenshots/stats` + `PATCH devices/{id}`.

### Phase 6 — Uji 10 PC + Hardening + Installer

Uji 10 PC; rate limiting, audit log, retention policy; otorisasi penuh endpoint screenshot/device; installer Inno Setup/WiX; dokumentasi privacy & admin.

## 19. Ringkasan Keputusan Kunci

| Area | Keputusan MVP |
|---|---|
| Database | MySQL server-side (EF Core); Postgres menyusul tanpa refactor |
| Screenshot storage | Local filesystem di balik `IScreenshotStore`; S3 menyusul |
| Screenshot transport | Endpoint terpisah (multipart), **bukan** base64 inline (hemat ~33% payload) |
| Device credential | Token acak per-device, disimpan **DPAPI** di agent, hanya **hash** di server |
| Identitas device | `machine_key` dari `MachineGuid`, **bukan** IP |
| Uptime | `GetTickCount64` (uptime OS) |
| Idempotency | `UNIQUE(device_id, client_uuid)` |
| Statistik | Dihitung **on-read**, tanpa tabel agregat |
| Capture | `Windows.Graphics.Capture` + fallback kompatibilitas (bukan stealth) |
| Audit / rate-limit / retention | Wajib; dikerjakan di **Phase 6** |
| DB engine di awal | Satu saja (MySQL), bukan dua opsi |
| API di Phase 1 | Hanya `register` + `heartbeat`; sisanya per-phase |
```

