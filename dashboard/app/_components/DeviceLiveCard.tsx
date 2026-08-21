'use client';

import useSWR from 'swr';
import { swrFetcher, DEVICE_DETAIL_POLL_INTERVAL_MS } from '@/lib/client-api';
import { formatDateTime, formatDuration, screenshotUrl } from '@/lib/api';
import type { Device } from '@/lib/api';

function InfoRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="meta-item">
      <span className="meta-key">{label}</span>
      <span className="meta-val">{value ?? '—'}</span>
    </div>
  );
}

// Polling tiap 60 detik — selaras dengan siklus report+screenshot agent, supaya status
// online/offline, app aktif, dan screenshot terakhir ter-update otomatis tanpa reload.
export default function DeviceLiveCard({ deviceId, initialDevice }: { deviceId: string; initialDevice: Device }) {
  const { data: device = initialDevice } = useSWR<Device>(`/api/devices/${deviceId}`, swrFetcher, {
    fallbackData: initialDevice,
    refreshInterval: DEVICE_DETAIL_POLL_INTERVAL_MS,
  });

  const isOnline = device.isOnline && device.isActive;

  return (
    <>
      <div className="page-header" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div>
          <h1 className="page-title">{device.hostname}</h1>
          <p className="page-sub">{device.deviceId}</p>
        </div>
        <span className={`pill ${isOnline ? 'pill-online' : 'pill-offline'}`} style={{ fontSize: 13 }}>
          <span className="pill-dot" />
          {isOnline ? 'Online' : device.isActive ? 'Offline' : 'Disabled'}
        </span>
      </div>

      {/* Device info */}
      <div className="card section">
        <div className="card-title">Informasi Device</div>
        <div className="meta-grid">
          <InfoRow label="IP Address"     value={<code style={{ fontFamily: 'monospace', color: 'var(--accent)' }}>{device.localIp}</code>} />
          <InfoRow label="Windows"        value={device.windowsVersion} />
          <InfoRow label="Agent"          value={device.agentVersion} />
          <InfoRow label="App Aktif"      value={device.lastActiveApp ? <span className="app-badge">{device.lastActiveApp}</span> : null} />
          <InfoRow label="Idle"           value={device.lastIdleSeconds != null ? formatDuration(device.lastIdleSeconds) : null} />
          <InfoRow label="Uptime"         value={device.lastUptimeSeconds != null ? formatDuration(device.lastUptimeSeconds) : null} />
          <InfoRow label="Last Seen"      value={device.lastSeenAt ? formatDateTime(device.lastSeenAt) : null} />
          <InfoRow label="Terdaftar"      value={formatDateTime(device.registeredAt)} />
        </div>
      </div>

      {/* Last screenshot */}
      {device.lastScreenshot && (
        <div className="card section">
          <div className="card-title">Screenshot Terakhir</div>
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img
            src={screenshotUrl(device.lastScreenshot.id)}
            alt="last screenshot"
            style={{ maxWidth: '100%', borderRadius: 8, border: '1px solid var(--border2)' }}
          />
          <div style={{ fontSize: 12, color: 'var(--text-muted)', marginTop: 8 }}>
            {formatDateTime(device.lastScreenshot.timestamp)}
          </div>
        </div>
      )}
    </>
  );
}
