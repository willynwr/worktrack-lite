'use client';

import { usePathname } from 'next/navigation';
import LogoutButton from './LogoutButton';

export default function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();

  if (pathname === '/login') {
    return <>{children}</>;
  }

  return (
    <div className="layout">
      <nav className="sidebar">
        <div className="sidebar-logo">
          <span className="logo-icon">⬡</span>
          <span className="logo-text">WorkTrack</span>
        </div>
        <div className="sidebar-nav">
          <a href="/" className="nav-item">
            <span className="nav-icon">◈</span> Overview
          </a>
          <a href="/devices" className="nav-item">
            <span className="nav-icon">▣</span> Devices
          </a>
        </div>
        <div className="sidebar-footer" style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          <LogoutButton />
          <span className="version-badge">v1.0.0</span>
        </div>
      </nav>
      <main className="main-content">{children}</main>
    </div>
  );
}
