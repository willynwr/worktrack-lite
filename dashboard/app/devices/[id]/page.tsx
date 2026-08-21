import { api } from '@/lib/server-api';
import { formatDuration } from '@/lib/api';
import type { DailyStats } from '@/lib/api';
import TimelineClient from './TimelineClient';
import DeviceLiveCard from '../../_components/DeviceLiveCard';

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

  return (
    <>
      <a href="/devices" className="back-link">← Semua Devices</a>

      <DeviceLiveCard deviceId={id} initialDevice={device} />

      {/* Stats */}
      {stats && <div className="section"><StatsPanel stats={stats} /></div>}

      {/* Timeline (interactive client component) */}
      <div className="card">
        <TimelineClient deviceId={id} />
      </div>
    </>
  );
}
