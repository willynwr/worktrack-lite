'use client';

import useSWR from 'swr';
import { swrFetcher, DEVICES_POLL_INTERVAL_MS } from '@/lib/client-api';
import { formatDateTime, formatDuration } from '@/lib/api';
import type { Device } from '@/lib/api';
import DeviceRow from './DeviceRow';

export default function DevicesClient({ initialDevices }: { initialDevices: Device[] }) {
  const { data: devices = initialDevices } = useSWR<Device[]>('/api/devices', swrFetcher, {
    fallbackData: initialDevices,
    refreshInterval: DEVICES_POLL_INTERVAL_MS,
  });

  return (
    <>
      <p className="page-sub" style={{ marginTop: -8, marginBottom: 16 }}>{devices.length} perangkat terdaftar</p>

      <div className="card">
        {devices.length === 0 ? (
          <div className="empty">Belum ada device terdaftar</div>
        ) : (
          <table className="device-table">
            <thead>
              <tr>
                <th>Status</th>
                <th>Hostname / ID</th>
                <th>IP</th>
                <th>OS</th>
                <th>Agent</th>
                <th>App Aktif</th>
                <th>Uptime</th>
                <th>Registered</th>
              </tr>
            </thead>
            <tbody>
              {devices.map(d => (
                <DeviceRow key={d.deviceId} deviceId={d.deviceId}>
                  <td>
                    <span className={`pill ${d.isOnline && d.isActive ? 'pill-online' : 'pill-offline'}`}>
                      <span className="pill-dot" />
                      {d.isOnline && d.isActive ? 'Online' : d.isActive ? 'Offline' : 'Disabled'}
                    </span>
                  </td>
                  <td>
                    <div style={{ fontWeight: 600 }}>{d.hostname}</div>
                    <div style={{ fontSize: 11, color: 'var(--text-muted)', fontFamily: 'monospace' }}>{d.deviceId}</div>
                  </td>
                  <td style={{ fontFamily: 'monospace', color: 'var(--text-dim)' }}>{d.localIp ?? '—'}</td>
                  <td style={{ color: 'var(--text-muted)', fontSize: 12 }}>{d.windowsVersion}</td>
                  <td style={{ color: 'var(--text-muted)', fontSize: 12 }}>{d.agentVersion}</td>
                  <td>
                    {d.lastActiveApp
                      ? <span className="app-badge">{d.lastActiveApp}</span>
                      : <span style={{ color: 'var(--text-muted)' }}>—</span>}
                  </td>
                  <td style={{ color: 'var(--text-dim)' }}>
                    {d.lastUptimeSeconds != null ? formatDuration(d.lastUptimeSeconds) : '—'}
                  </td>
                  <td style={{ color: 'var(--text-muted)', fontSize: 12 }}>
                    {formatDateTime(d.registeredAt)}
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
