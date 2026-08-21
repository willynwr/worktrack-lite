// Types dan formatter murni — aman diimpor dari Server Component maupun Client Component.
// Fetcher yang butuh admin token ada di lib/server-api.ts (Server Component) dan
// lib/client-api.ts (Client Component, lewat proxy route lokal di app/api/*).

export interface Device {
  deviceId: string;
  hostname: string;
  localIp: string | null;
  windowsVersion: string;
  agentVersion: string;
  registeredAt: string;
  lastSeenAt: string | null;
  isActive: boolean;
  isOnline: boolean;
  lastActiveApp: string | null;
  lastIdleSeconds: number | null;
  lastUptimeSeconds: number | null;
  lastReportAt: string | null;
  lastScreenshot?: { id: number; fileUrl: string; timestamp: string } | null;
}

export interface TimelineEntry {
  timestamp: string;
  activeApp: string | null;
  idleSeconds: number;
  uptimeSeconds: number;
  screenshot: { id: number; fileUrl: string; sizeBytes: number } | null;
}

export interface TimelineResponse {
  date: string;
  timeline: TimelineEntry[];
}

export interface AppUsage {
  app: string;
  minutes: number;
}

export interface DailyStats {
  date: string;
  totalRecords: number;
  totalActiveSeconds: number;
  totalIdleSeconds: number;
  maxUptimeSeconds: number;
  screenshotCount: number;
  topApps: AppUsage[];
}

// ── Screenshot: selalu lewat proxy lokal (app/api/screenshot/[id]) supaya token
// admin (httpOnly cookie di domain dashboard) tidak perlu dikirim ke origin API lain. ──
export function screenshotUrl(id: number): string {
  return `/api/screenshot/${id}`;
}

// ── Formatters ───────────────────────────────────────────────────────────────

export function formatDuration(seconds: number): string {
  if (seconds < 60) return `${seconds}s`;
  if (seconds < 3600) return `${Math.floor(seconds / 60)}m`;
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  return m > 0 ? `${h}h ${m}m` : `${h}h`;
}

export function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString('id-ID', { hour: '2-digit', minute: '2-digit' });
}

export function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString('id-ID', {
    day: '2-digit', month: 'short', year: 'numeric',
    hour: '2-digit', minute: '2-digit',
  });
}

export function today(): string {
  return new Date().toISOString().slice(0, 10);
}
