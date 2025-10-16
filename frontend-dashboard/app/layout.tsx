import './globals.css'
import type { ReactNode } from 'react'
import { SiteShell } from '@/components/layout/site-shell'
import { inter } from '@/lib/fonts'

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="tr" suppressHydrationWarning className={inter.variable}>
      <body className="font-sans">
        <SiteShell>{children}</SiteShell>
      </body>
    </html>
  )
}
