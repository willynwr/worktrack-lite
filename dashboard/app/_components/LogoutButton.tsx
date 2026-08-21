'use client';

import { useRouter } from 'next/navigation';

export default function LogoutButton() {
  const router = useRouter();

  async function handleLogout() {
    await fetch('/api/session', { method: 'DELETE' });
    router.replace('/login');
    router.refresh();
  }

  return (
    <button className="btn btn-danger" style={{ width: '100%', justifyContent: 'center' }} onClick={handleLogout}>
      Logout
    </button>
  );
}
