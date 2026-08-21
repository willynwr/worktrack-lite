'use client';

import useSWR from 'swr';
import { swrFetcher, DEVICES_POLL_INTERVAL_MS } from '@/lib/client-api';
import { formatDateTime, formatDuration } from '@/lib/api';
import type { Device } from '@/lib/api';
import DeviceRow from './DeviceRow';

function StatCard({ label, value, sub }: { label: string; value: string | number; sub?: string }) {
  return (
    <div className="stat-card">
      <div className="stat-label">{label}</div>
      <div className="stat-value">{value}</div>
      {sub && <div className="stat-sub">{sub}</div>}
    </div>
  );
}

export default function OverviewClient({ initialDevices }: { initialDevices: Device[] }) {
  // Polling tiap 5 detik supaya status online/offline & app aktif tidak nyangkut
  // saat PC/agent mati (sebelumnya halaman ini cuma fetch sekali saat load).
  const { data: devices = initialDevices } = useSWR<Device[]>('/api/devices', swrFetcher, {
    fallbackData: initialDevices,
    refreshInterval: DEVICES_POLL_INTERVAL_MS,
  });

  const online = devices.filter(d => d.isOnline && d.isActive).length;
  const total  = devices.length;

  return (
    <>
      <div className="stats-grid">
        <StatCard label="Total Devices" value={total} />
        <StatCard label="Online Now"    value={online} sub={`${total - online} offline`} />
        <StatCard label="Active"        value={devices.filter(d => d.isActive).length} />
      </div>

      <div className="card">
        <div className="card-title">Semua Perangkat</div>
        {devices.length === 0 ? (
          <div className="empty">Belum ada device yang terdaftar</div>
        ) : (
          <table className="device-table">
            <thead>
              <tr>
                <th>Status</th>
                <th>Device</th>
                <th>IP</th>
                <th>App Aktif</th>
                <th>Idle</th>
                <th>Uptime</th>
                <th>Last Seen</th>
              </tr>
            </thead>
            <tbody>
              {devices.map(d => (
                <DeviceRow key={d.deviceId} deviceId={d.deviceId}>
                  <td>
                    <span className={`pill ${d.isOnline && d.isActive ? 'pill-online' : 'pill-offline'}`}>
                      <span className="pill-dot" />
                      {d.isOnline && d.isActive ? 'Online' : 'Offline'}
                    </span>
                  </td>
                  <td>
                    <div style={{ fontWeight: 600 }}>{d.hostname}</div>
                    <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>{d.deviceId}</div>
                  </td>
                  <td style={{ color: 'var(--text-dim)', fontFamily: 'monospace' }}>{d.localIp ?? '—'}</td>
                  <td>
                    {d.lastActiveApp
                      ? <span className="app-badge">{d.lastActiveApp}</span>
                      : <span style={{ color: 'var(--text-muted)' }}>—</span>}
                  </td>
                  <td style={{ color: 'var(--text-dim)' }}>
                    {d.lastIdleSeconds != null ? formatDuration(d.lastIdleSeconds) : '—'}
                  </td>
                  <td style={{ color: 'var(--text-dim)' }}>
                    {d.lastUptimeSeconds != null ? formatDuration(d.lastUptimeSeconds) : '—'}
                  </td>
                  <td style={{ color: 'var(--text-muted)', fontSize: 12 }}>
                    {d.lastSeenAt ? formatDateTime(d.lastSeenAt) : '—'}
                  </td>
                </DeviceRow>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </>
  );
}
