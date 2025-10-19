"use client"
import './globals.css'
import type { ReactNode } from 'react'
import { usePathname } from 'next/navigation'
import { SiteShell } from '@/components/layout/site-shell'
import { inter } from '@/lib/fonts'

export default function RootLayout({ children }: { children: ReactNode }) {
  const pathname = usePathname()
  const isLogin = pathname?.startsWith('/login')
  return (
    <html lang="tr" suppressHydrationWarning className={inter.variable}>
      <head>
        <meta charSet="utf-8" />
        <meta httpEquiv="Content-Type" content="text/html; charset=utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
      </head>
      <body className="font-sans">
        {isLogin ? children : <SiteShell>{children}</SiteShell>}
      </body>
    </html>
  )
}
