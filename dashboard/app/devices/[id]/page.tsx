import { api } from '@/lib/server-api';
import { formatDateTime, formatDuration, screenshotUrl } from '@/lib/api';
import type { Device, DailyStats } from '@/lib/api';
import TimelineClient from './TimelineClient';

interface Props { params: Promise<{ id: string }> }

async function getData(id: string) {
  const todayStr = new Date().toISOString().slice(0, 10);
  const [device, stats] = await Promise.allSettled([
    api.device(id),
    api.stats(id, todayStr),
  ]);
  return {
    device: device.status === 'fulfilled' ? device.value : null,
    stats:  stats.status  === 'fulfilled' ? stats.value  : null,
  };
}

function InfoRow({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="meta-item">
      <span className="meta-key">{label}</span>
      <span className="meta-val">{value ?? '—'}</span>
    </div>
  );
}

function StatsPanel({ stats }: { stats: DailyStats }) {
  const maxMin = stats.topApps[0]?.minutes ?? 1;
  return (
    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
      {/* Left: summary */}
      <div className="card">
        <div className="card-title">Statistik Hari Ini</div>
        <div className="stats-grid" style={{ marginBottom: 0 }}>
          <div className="stat-card">
            <div className="stat-label">Aktif</div>
            <div className="stat-value">{formatDuration(stats.totalActiveSeconds)}</div>
          </div>
          <div className="stat-card">
            <div className="stat-label">Idle</div>
            <div className="stat-value">{formatDuration(stats.totalIdleSeconds)}</div>
          </div>
          <div className="stat-card">
            <div className="stat-label">Uptime</div>
            <div className="stat-value">{formatDuration(stats.maxUptimeSeconds)}</div>
          </div>
          <div className="stat-card">
            <div className="stat-label">Screenshots</div>
            <div className="stat-value">{stats.screenshotCount}</div>
          </div>
        </div>
      </div>

      {/* Right: top apps */}
      <div className="card">
        <div className="card-title">Top Apps Hari Ini</div>
        {stats.topApps.length === 0 ? (
          <div style={{ color: 'var(--text-muted)', fontSize: 13 }}>Belum ada data</div>
        ) : (
          <div className="bar-list">
            {stats.topApps.map(a => (
              <div className="bar-row" key={a.app}>
                <span className="bar-label" title={a.app}>{a.app}</span>
                <div className="bar-track">
                  <div className="bar-fill" style={{ width: `${(a.minutes / maxMin) * 100}%` }} />
                </div>
                <span className="bar-val">{a.minutes}m</span>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

export default async function DeviceDetailPage({ params }: Props) {
  const { id } = await params;
  const { device, stats } = await getData(id);

  if (!device) {
    return (
      <div>
        <a href="/devices" className="back-link">← Kembali</a>
        <div className="empty">Device tidak ditemukan</div>
      </div>
    );
  }

  const isOnline = device.isOnline && device.isActive;

  return (
    <>
      <a href="/devices" className="back-link">← Semua Devices</a>

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

      {/* Stats */}
      {stats && <div className="section"><StatsPanel stats={stats} /></div>}

      {/* Timeline (interactive client component) */}
      <div className="card">
        <TimelineClient deviceId={id} />
      </div>
    </>
  );
}
