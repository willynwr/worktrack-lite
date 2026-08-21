'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';

export default function LoginPage() {
  const router = useRouter();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError]       = useState<string | null>(null);
  const [pending, setPending]   = useState(false);

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    setError(null);
    setPending(true);
    try {
      const res = await fetch('/api/session', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username, password }),
      });
      const data = await res.json();
      if (!res.ok) {
        setError(data.error ?? 'Login gagal.');
        return;
      }
      router.replace('/');
      router.refresh();
    } catch {
      setError('Tidak dapat terhubung ke server.');
    } finally {
      setPending(false);
    }
  }

  return (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', minHeight: '100vh' }}>
      <form onSubmit={handleSubmit} className="card" style={{ width: 340 }}>
        <div className="card-title" style={{ fontSize: 18, marginBottom: 16 }}>
          <span style={{ marginRight: 8 }}>⬡</span>WorkTrack Admin
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          <input
            className="date-input"
            style={{ width: '100%' }}
            type="text"
            placeholder="Username"
            value={username}
            onChange={e => setUsername(e.target.value)}
            autoFocus
            required
          />
          <input
            className="date-input"
            style={{ width: '100%' }}
            type="password"
            placeholder="Password"
            value={password}
            onChange={e => setPassword(e.target.value)}
            required
          />

          {error && <div style={{ color: 'var(--danger)', fontSize: 13 }}>{error}</div>}

          <button type="submit" className="btn btn-primary" disabled={pending} style={{ justifyContent: 'center' }}>
            {pending ? 'Memproses...' : 'Login'}
          </button>
        </div>
      </form>
    </div>
  );
}
