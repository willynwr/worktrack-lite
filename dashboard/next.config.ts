import type { NextConfig } from 'next';

const nextConfig: NextConfig = {
  // Allow fetching screenshots from API server as image sources
  images: {
    remotePatterns: [
      { protocol: 'http', hostname: 'localhost', port: '5031', pathname: '/api/v1/screenshots/**' },
    ],
  },
  // Proxy /api/* to backend during dev (optional — using env var directly instead)
};

export default nextConfig;
