# Pernyataan Privasi — WorkTrack Lite

Dokumen ini menjelaskan data apa yang dikumpulkan oleh WorkTrack Lite, untuk tujuan apa,
berapa lama disimpan, dan siapa yang dapat mengaksesnya. WorkTrack Lite **hanya boleh
dipasang pada PC milik perusahaan** yang digunakan untuk keperluan kerja, dan pemasangannya
wajib diberitahukan kepada karyawan sesuai kebijakan perusahaan dan peraturan
ketenagakerjaan/perlindungan data yang berlaku di yurisdiksi masing-masing.

## 1. Data yang dikumpulkan

| Data | Sumber | Frekuensi |
|---|---|---|
| Nama aplikasi yang sedang aktif (foreground window) | `GetForegroundWindow` | Setiap 60 detik |
| Waktu idle (tidak ada input mouse/keyboard) | `GetLastInputInfo` | Setiap 60 detik |
| Uptime PC | `GetTickCount64` | Setiap 60 detik |
| Screenshot layar (JPEG) | Capture layar (GDI) | Berkala, sesuai konfigurasi agent |
| Metadata device: hostname, versi Windows, versi agent, IP lokal | Saat registrasi & heartbeat | Saat registrasi, lalu setiap heartbeat |

## 2. Data yang **TIDAK** dikumpulkan

WorkTrack Lite secara desain **tidak** melakukan:

- Keylogging (perekaman tombol yang ditekan).
- Penangkapan kredensial (password, token, isi form login).
- Perekaman isi clipboard.
- Perekaman audio atau webcam.
- Pembacaan isi dokumen, email, atau pesan pribadi.

## 3. Tujuan pengumpulan data

Data digunakan semata-mata untuk:

- Memantau produktivitas dan pemakaian perangkat kerja perusahaan (aplikasi yang digunakan, waktu aktif vs idle).
- Membantu tim IT/administrasi memverifikasi perangkat dalam kondisi baik (uptime, konektivitas).
- Menyediakan bukti aktivitas kerja (screenshot berkala) sesuai kebijakan internal perusahaan.

Data **tidak** digunakan untuk tujuan lain di luar yang disebutkan di atas, dan tidak dibagikan ke pihak ketiga.

## 4. Retensi data

- Screenshot disimpan maksimum **30 hari** (dapat dikonfigurasi via `Admin:RetentionDays` di `appsettings.json` server), setelah itu dihapus otomatis oleh `RetentionService` yang berjalan setiap 24 jam.
- Data aktivitas (`ActivityReports`) dan metadata device mengikuti kebijakan retensi yang sama, kecuali diatur berbeda oleh administrator.

## 5. Siapa yang dapat melihat data

- Hanya akun **admin** yang terdaftar di sistem (`AdminUsers`) yang dapat login ke dashboard dan melihat data device, timeline aktivitas, dan screenshot.
- Setiap akses dashboard mensyaratkan autentikasi JWT admin (`AdminJwtFilter`), tidak ada akses anonim.
- Aksi admin yang sensitif (mis. menonaktifkan/mengaktifkan device) dicatat ke tabel audit log (`AuditLogs`) berisi: username admin, aksi, target, waktu, dan alamat IP — lihat [admin-guide.md](admin-guide.md#audit-log).

## 6. Cara administrator menonaktifkan device

Administrator dapat menonaktifkan pemantauan suatu PC melalui dashboard (`PATCH /api/v1/dashboard/devices/{id}` dengan `is_active: false`), atau langsung melalui database. Device yang dinonaktifkan:

- Ditolak saat mengirim heartbeat maupun laporan aktivitas/screenshot baru (HTTP 403).
- Tetap tercatat historisnya di dashboard sampai data tersebut kedaluwarsa sesuai kebijakan retensi.

Untuk menghentikan pemantauan sepenuhnya, uninstall agent melalui Control Panel (installer WorkTrack menyediakan entri uninstall standar Windows) — lihat [admin-guide.md](admin-guide.md#uninstall-agent).

## 7. Transparansi kepada pengguna PC

WorkTrack Lite tidak dirancang untuk beroperasi secara tersembunyi dari administrator sistem: proses agent (`WorkTrack.Service.exe`, `WorkTrack.SessionAgent.exe`) terlihat normal di Task Manager, dan tidak ada mekanisme untuk menyembunyikannya dari user, antivirus, atau software keamanan. Perusahaan bertanggung jawab untuk memberi tahu karyawan tentang pemasangan software ini sesuai kebijakan dan hukum yang berlaku.

## 8. Kepatuhan

Penggunaan WorkTrack Lite harus sesuai dengan hukum ketenagakerjaan dan perlindungan data yang berlaku (mis. pemberitahuan kepada karyawan, batasan jam kerja yang dipantau, dsb). WorkTrack Lite tidak menyediakan fitur untuk menghindari kewajiban hukum, consent/notice, atau kebijakan perusahaan terkait pemantauan karyawan — kepatuhan tersebut menjadi tanggung jawab perusahaan yang mengoperasikan sistem ini.
