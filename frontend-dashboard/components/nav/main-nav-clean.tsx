"use client"
import Link from 'next/link'
import { useEffect, useState } from 'react'
import { usePathname } from 'next/navigation'
import { IconExpense, IconHome, IconInvoice } from '@/components/ui/icons'

type NavItem = { href: string; label: string; icon: (p: { width?: number; height?: number }) => JSX.Element }

const baseItems: NavItem[] = [
  { href: '/', label: 'Ana Sayfa', icon: IconHome },
  { href: '/invoices', label: 'Faturalar', icon: IconInvoice },
  { href: '/expenses', label: 'Giderler', icon: IconExpense },
  { href: '/reports', label: 'Raporlar', icon: IconHome },
]

export function MainNav({ onNavigate }: { onNavigate?: () => void }) {
  const pathname = usePathname()
  const [role, setRole] = useState<string | null>(null)
  const [perms, setPerms] = useState<{ canAccessLeavesAdmin?: boolean; canManageSettings?: boolean; canManageCashier?: boolean; canManageKarat?: boolean; canUseInvoices?: boolean; canUseExpenses?: boolean; canViewReports?: boolean } | null>(null)

  useEffect(() => {
    try { setRole(localStorage.getItem('ktp_role')) } catch {}
    ;(async () => {
      try {
        const base = process.env.NEXT_PUBLIC_API_BASE || ''
        const token = typeof window !== 'undefined' ? (localStorage.getItem('ktp_token') || '') : ''
        const res = await fetch(`${base}/api/me/permissions`, { cache: 'no-store', headers: token ? { Authorization: `Bearer ${token}` } : {} })
        if (res.ok) { const j = await res.json(); setPerms(j); if (!role && j.role) setRole(j.role) }
      } catch {}
    })()
  }, [role])

  const adminItems: NavItem[] = [
    ...(perms?.canManageCashier ? [{ href: '/cashiers', label: 'Kasiyerler', icon: IconHome } as NavItem] : []),
    ...(perms?.canAccessLeavesAdmin ? [{ href: '/leaves', label: 'Izin Yonetimi', icon: IconHome } as NavItem] : []),
    ...(perms?.canManageCashier ? [{ href: '/users/permissions', label: 'Kullanici Yetkileri', icon: IconHome } as NavItem] : []),
    ...(perms?.canManageCashier ? [{ href: '/users/roles', label: 'Roller', icon: IconHome } as NavItem] : []),
    ...(perms?.canManageSettings ? [{ href: '/settings', label: 'Ayarlar', icon: IconHome } as NavItem] : []),
    ...(perms?.canManageSettings ? [{ href: '/settings/pricing', label: 'Fiyat Ayarları', icon: IconHome } as NavItem] : []),
    ...(perms?.canManageKarat ? [{ href: '/settings/karat', label: 'Karat Ayarlari', icon: IconHome } as NavItem] : []),
  ]

  const items: NavItem[] = [
    ...baseItems.filter(it => {
      if (it.href === '/' && perms && perms.canViewReports === false) return false
      if (it.href === '/invoices' && perms && perms.canUseInvoices === false) return false
      if (it.href === '/expenses' && perms && perms.canUseExpenses === false) return false
      if (it.href === '/reports' && perms && perms.canViewReports === false) return false
      return true
    }),
    ...adminItems
  ]

  return (
    <nav className="flex flex-col gap-1">
      {items.map((it) => {
        const active = pathname === it.href
        const Icon = it.icon
        return (
          <Link
            key={it.href}
            href={it.href}
            onClick={onNavigate}
            className={`flex items-center gap-3 rounded-md px-3 py-2 text-sm transition-colors ${active ? 'bg-primary text-primary-foreground' : 'hover:bg-accent'}`}
          >
            <Icon width={18} height={18} />
            {it.label}
          </Link>
        )
      })}
    </nav>
  )
}




