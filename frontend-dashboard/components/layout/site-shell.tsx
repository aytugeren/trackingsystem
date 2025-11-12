"use client"
import { useEffect, useState } from 'react'
import Link from 'next/link'
import { useRouter } from 'next/navigation'
import { MainNav } from '@/components/nav/main-nav-clean'
import { ThemeToggle } from '@/components/ui/theme-toggle'
import GlobalKaratAlertFixed from '@/components/global-karat-alert-fixed'
import { IconMenu } from '@/components/ui/icons'

export function SiteShell({ children }: { children: React.ReactNode }) {
  const [open, setOpen] = useState(false)
  const router = useRouter()

  useEffect(() => {
    try {
      const token = localStorage.getItem('ktp_token')
      if (!token) router.replace('/login')
    } catch {
      router.replace('/login')
    }
  }, [router])

  useEffect(() => {
    if (open) {
      const prev = document.body.style.overflow
      document.body.style.overflow = 'hidden'
      return () => { document.body.style.overflow = prev }
    }
  }, [open])

  function logout() {
    try {
      localStorage.removeItem('ktp_token')
      localStorage.removeItem('ktp_role')
      localStorage.removeItem('ktp_email')
      document.cookie = 'ktp_token=; Max-Age=0; path=/'
    } finally {
      router.replace('/login')
    }
  }

  return (
    <div className="grid min-h-dvh grid-cols-1 md:grid-cols-[288px_1fr]">
      {/* Sidebar */}
      <aside className={`fixed inset-y-0 left-0 z-50 w-72 shrink-0 border-r bg-white/80 backdrop-blur transition-transform duration-200 ease-out md:static md:translate-x-0 dark:bg-background ${open ? 'translate-x-0' : '-translate-x-full'}`}>
        <div className="h-14 border-b px-4 flex items-center font-semibold tracking-tight">KTP Dashboard</div>
        <div className="p-3">
          <MainNav onNavigate={() => setOpen(false)} />
        </div>
      </aside>

      {/* Overlay for mobile */}
      {open && <div className="fixed inset-0 z-40 bg-black/40 md:hidden" onClick={() => setOpen(false)} />}

      {/* Content area */}
      <div className="flex min-w-0 flex-col">
        <header className="sticky top-0 z-30 h-14 border-b bg-white/80 backdrop-blur dark:bg-background">
          <div className="flex h-full items-center justify-between gap-3 px-4">
            <button aria-label="Menü" className="md:hidden h-9 w-9 inline-flex items-center justify-center rounded-md border hover:bg-accent" onClick={() => setOpen(s => !s)}>
              <IconMenu width={18} height={18} />
            </button>
            <div className="hidden md:block text-sm text-muted-foreground">Yönetim</div>
            <div className="ml-auto flex items-center gap-2">
              <ThemeToggle />
              <button onClick={logout} className="h-9 inline-flex items-center rounded-md border px-3 text-sm hover:bg-accent">Çıkış</button>
              <Link href="/invoices" className="hidden sm:inline-flex h-9 items-center rounded-md border px-3 text-sm hover:bg-accent">Hızlı Erişim: Faturalar</Link>
            </div>
          </div>
        </header>
        <GlobalKaratAlertFixed />
        <main className="flex-1 p-4 md:p-6">{children}</main>
      </div>
    </div>
  )
}
