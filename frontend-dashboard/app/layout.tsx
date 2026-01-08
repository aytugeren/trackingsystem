"use client"
import './globals.css'
import type { ReactNode } from 'react'
import { useEffect } from 'react'
import { usePathname } from 'next/navigation'
import { SiteShell } from '@/components/layout/site-shell'
import { inter } from '@/lib/fonts'
import { Toaster } from 'sonner'
import Swal from 'sweetalert2'
import 'sweetalert2/dist/sweetalert2.min.css'

export default function RootLayout({ children }: { children: ReactNode }) {
  const pathname = usePathname()
  const isLogin = pathname?.startsWith('/login')
  useEffect(() => {
    try {
      const flag = sessionStorage.getItem('ktp_login_success')
      if (flag) {
        sessionStorage.removeItem('ktp_login_success')
        Swal.fire({
          icon: 'success',
          title: 'Giriş başarılı',
          text: 'Hoş geldiniz.',
          timer: 1800,
          showConfirmButton: false,
          showClass: { popup: 'swal2-show' },
          hideClass: { popup: 'swal2-hide' }
        })
      }
    } catch {}
  }, [])
  return (
    <html lang="tr" suppressHydrationWarning className={inter.variable}>
      <head>
        <meta charSet="utf-8" />
        <meta httpEquiv="Content-Type" content="text/html; charset=utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
      </head>
      <body className="font-sans">
        {isLogin ? children : <SiteShell>{children}</SiteShell>}
        <Toaster richColors position="top-right" closeButton />
      </body>
    </html>
  )
}
