'use client';

import { useState } from 'react';

function toDateStr(d: Date): string {
  return d.toISOString().slice(0, 10);
}

function daysAgo(n: number): string {
  const d = new Date();
  d.setDate(d.getDate() - n);
  return toDateStr(d);
}

const PRESETS = [
  { label: 'Hari Ini', from: () => daysAgo(0) },
  { label: '3 Hari Terakhir', from: () => daysAgo(2) },
  { label: '7 Hari Terakhir', from: () => daysAgo(6) },
];

export default function DownloadScreenshotsButton({ deviceId }: { deviceId: string }) {
  const [downloading, setDownloading] = useState<string | null>(null);

  function download(label: string, from: string) {
    setDownloading(label);
    const to = toDateStr(new Date());
    // Navigasi biasa (bukan fetch) — browser yang handle save dialog dari
    // Content-Disposition: attachment, sekaligus otomatis kirim cookie same-origin.
    window.location.href = `/api/devices/${deviceId}/screenshots/download?from=${from}&to=${to}`;
    setTimeout(() => setDownloading(null), 2000);
  }

  return (
    <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
      {PRESETS.map(p => (
        <button
          key={p.label}
          className="btn btn-ghost"
          disabled={downloading !== null}
          onClick={() => download(p.label, p.from())}
        >
          {downloading === p.label ? 'Menyiapkan ZIP...' : `⬇ ${p.label}`}
        </button>
      ))}
    </div>
  );
}
