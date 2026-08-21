import { api } from '@/lib/server-api';
import type { Device } from '@/lib/api';
import DevicesClient from '../_components/DevicesClient';

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
      </div>

      <DevicesClient initialDevices={devices} />
    </>
  );
}
