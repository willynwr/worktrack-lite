'use client';

import { useState } from 'react';
import { fetchTimeline } from '@/lib/client-api';
import { formatTime, screenshotUrl } from '@/lib/api';
import type { TimelineEntry } from '@/lib/api';

interface Props {
  deviceId: string;
}

export default function TimelineClient({ deviceId }: Props) {
  const [date, setDate]         = useState(() => new Date().toISOString().slice(0, 10));
  const [timeline, setTimeline] = useState<TimelineEntry[] | null>(null);
  const [loading, setLoading]   = useState(false);
  const [modal, setModal]       = useState<string | null>(null);

  const load = async (d: string) => {
    setLoading(true);
    try {
      const r = await fetchTimeline(deviceId, d);
      setTimeline(r.timeline);
    } catch {
      setTimeline([]);
    } finally {
      setLoading(false);
    }
  };

  const handleDate = (e: React.ChangeEvent<HTMLInputElement>) => {
    setDate(e.target.value);
  };

  const handleLoad = () => load(date);

  return (
    <div className="section">
      <div className="section-header">
        <div className="section-title">Screenshot Timeline</div>
        <div style={{ display: 'flex', gap: 8 }}>
          <input type="date" className="date-input" value={date} onChange={handleDate} />
          <button className="btn btn-primary" onClick={handleLoad}>Tampilkan</button>
        </div>
      </div>

      {loading && <div className="loading">Memuat timeline</div>}

      {!loading && timeline !== null && timeline.length === 0 && (
        <div className="empty">Tidak ada data untuk tanggal ini</div>
      )}

      {!loading && timeline && timeline.length > 0 && (
        <div className="timeline-grid">
          {timeline.map((entry, i) => {
            const isIdle = entry.idleSeconds >= 60;
            const imgUrl = entry.screenshot ? screenshotUrl(entry.screenshot.id) : null;
            return (
              <div
                key={i}
                className={`timeline-item ${isIdle ? 'idle' : ''}`}
                onClick={() => imgUrl && setModal(imgUrl)}
                title={`${formatTime(entry.timestamp)} — ${entry.activeApp ?? 'idle'} | idle ${entry.idleSeconds}s`}
              >
                {imgUrl ? (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img src={imgUrl} alt={entry.activeApp ?? 'screenshot'} className="timeline-thumb" loading="lazy" />
                ) : (
                  <div className="timeline-thumb-placeholder">⬜</div>
                )}
                <div className="timeline-meta">
                  <div className="timeline-time">{formatTime(entry.timestamp)}</div>
                  <div className="timeline-app">{isIdle ? '💤 Idle' : (entry.activeApp ?? '—')}</div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {modal && (
        <div className="modal-overlay" onClick={() => setModal(null)}>
          <button className="modal-close" onClick={() => setModal(null)}>✕</button>
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img src={modal} alt="screenshot" className="modal-img" onClick={e => e.stopPropagation()} />
        </div>
      )}
    </div>
  );
}
