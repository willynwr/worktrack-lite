import { api } from '@/lib/server-api';
import { formatDateTime, formatDuration } from '@/lib/api';
import type { Device } from '@/lib/api';
import DeviceRow from '../_components/DeviceRow';

async function getDevices(): Promise<Device[]> {
  try { return await api.devices(); }
  catch { return []; }
}

export default async function DevicesPage() {
  const devices = await getDevices();

  return (
    <>
      <div className="page-header">
        <h1 className="page-title">Devices</h1>
        <p className="page-sub">{devices.length} perangkat terdaftar</p>
      </div>

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
