import type { NextConfig } from 'next'

const nextConfig: NextConfig = {
  reactStrictMode: true,
  async rewrites() {
    const target = process.env.API_REWRITE_TARGET || 'http://api:8080'
    return [
      {
        source: '/api/:path*',
        destination: `${target}/api/:path*`,
      },
    ]
  },
}

export default nextConfig
