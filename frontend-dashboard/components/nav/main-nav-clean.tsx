"use client"
import Link from 'next/link'
import { useEffect, useState } from 'react'
import { usePathname } from 'next/navigation'
import { IconCalendar, IconChart, IconCog, IconExpense, IconHome, IconInvoice, IconShield, IconTag, IconTarget, IconUsers } from '@/components/ui/icons'
import { cn } from '@/components/ui/cn'

type NavItem = { href: string; label: string; icon: (p: { width?: number; height?: number }) => JSX.Element }

const baseItems: NavItem[] = [
  { href: '/', label: 'Ana Sayfa', icon: IconHome },
  { href: '/invoices', label: 'Faturalar', icon: IconInvoice },
  { href: '/expenses', label: 'Giderler', icon: IconExpense },
  { href: '/reports', label: 'Raporlar', icon: IconChart },
  { href: '/etiket-basma', label: 'Etiket Basma', icon: IconTag },
]

export function MainNav({ onNavigate, collapsed = false }: { onNavigate?: () => void; collapsed?: boolean }) {
  const pathname = usePathname()
  const [role, setRole] = useState<string | null>(null)
  const [perms, setPerms] = useState<{ canAccessLeavesAdmin?: boolean; canManageSettings?: boolean; canManageCashier?: boolean; canManageKarat?: boolean; canUseInvoices?: boolean; canUseExpenses?: boolean; canViewReports?: boolean; canPrintLabels?: boolean } | null>(null)

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
    ...(perms?.canManageCashier ? [{ href: '/cashiers', label: 'Kasiyerler', icon: IconUsers } as NavItem] : []),
    ...(perms?.canAccessLeavesAdmin ? [{ href: '/leaves', label: 'Izin Yonetimi', icon: IconCalendar } as NavItem] : []),
    ...(perms?.canManageCashier ? [{ href: '/users/permissions', label: 'Kullanici Yetkileri', icon: IconShield } as NavItem] : []),
    ...(perms?.canManageCashier ? [{ href: '/users/roles', label: 'Roller', icon: IconTarget } as NavItem] : []),
    ...(perms?.canManageSettings ? [{ href: '/settings', label: 'Ayarlar', icon: IconCog } as NavItem] : []),
    ...(perms?.canManageSettings ? [{ href: '/settings/pricing', label: 'Fiyat Ayarları', icon: IconChart } as NavItem] : []),
    ...(perms?.canManageKarat ? [{ href: '/settings/karat', label: 'Karat Ayarlari', icon: IconTarget } as NavItem] : []),
  ]

  const items: NavItem[] = [
    ...baseItems.filter(it => {
      if (it.href === '/' && perms && perms.canViewReports === false) return false
      if (it.href === '/invoices' && perms && perms.canUseInvoices === false) return false
      if (it.href === '/expenses' && perms && perms.canUseExpenses === false) return false
      if (it.href === '/reports' && perms && perms.canViewReports === false) return false
      if (it.href === '/etiket-basma' && perms && perms.canPrintLabels === false) return false
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
            title={it.label}
            className={cn(
              'flex items-center rounded-md px-3 py-2 text-sm transition-colors',
              active ? 'bg-primary text-primary-foreground' : 'hover:bg-accent',
              collapsed ? 'justify-center gap-0' : 'gap-3'
            )}
          >
            <Icon width={18} height={18} />
            <span className={collapsed ? 'sr-only' : undefined}>{it.label}</span>
          </Link>
        )
      })}
    </nav>
  )
}
