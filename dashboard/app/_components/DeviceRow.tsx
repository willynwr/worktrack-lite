'use client';

import { useRouter } from 'next/navigation';

export default function DeviceRow({ deviceId, children }: { deviceId: string; children: React.ReactNode }) {
  const router = useRouter();
  return (
    <tr onClick={() => router.push(`/devices/${deviceId}`)} style={{ cursor: 'pointer' }}>
      {children}
    </tr>
  );
}
