import type { NextConfig } from 'next'

// Prefer explicit rewrite target, then public API URL, then localhost for local dev
const API_URL = process.env.API_REWRITE_TARGET || process.env.NEXT_PUBLIC_API_URL || 'http://api:8080'

const nextConfig: NextConfig = {
  reactStrictMode: true,
  async rewrites() {
    return [
      {
        source: '/api/:path*',
        destination: `${API_URL}/api/:path*`,
      },
    ]
  },
}

export default nextConfig
