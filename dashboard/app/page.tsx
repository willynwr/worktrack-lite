import { api } from '@/lib/server-api';
import { requireAdmin } from '@/lib/session';
import type { Device } from '@/lib/api';
import OverviewClient from './_components/OverviewClient';

async function getDevices(): Promise<Device[]> {
  try { return await api.devices(); }
  catch { return []; }
}

export default async function HomePage() {
  await requireAdmin();
  const devices = await getDevices();

  return (
    <>
      <div className="page-header">
        <h1 className="page-title">Overview</h1>
        <p className="page-sub">Status real-time semua perangkat yang terdaftar</p>
      </div>

      <OverviewClient initialDevices={devices} />
    </>
  );
}
