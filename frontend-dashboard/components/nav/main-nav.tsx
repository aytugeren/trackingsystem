"use client"
import Link from 'next/link'
import { useEffect, useState } from 'react'
import { usePathname } from 'next/navigation'
import { IconExpense, IconHome, IconInvoice } from '@/components/ui/icons'

const baseItems = [
  { href: '/', label: 'Ana Sayfa', icon: IconHome },
  { href: '/invoices', label: 'Faturalar', icon: IconInvoice },
  { href: '/expenses', label: 'Giderler', icon: IconExpense },
  { href: '/reports', label: 'Raporlar', icon: IconHome },
]

export function MainNav({ onNavigate }: { onNavigate?: () => void }) {
  const pathname = usePathname()
  const [role, setRole] = useState<string | null>(null)
  useEffect(() => {
    try { setRole(localStorage.getItem('ktp_role')) } catch {}
  }, [])

  return (
    <nav className="flex flex-col gap-1">
      {[...baseItems, ...(role === 'Yonetici' ? [{ href: '/cashiers', label: 'Kasiyerler', icon: IconHome }] as const : [])].map((it) => {
        const active = pathname === it.href
        const Icon = it.icon
        return (
          <Link
            key={it.href}
            href={it.href}
            onClick={onNavigate}
            className={`flex items-center gap-3 rounded-md px-3 py-2 text-sm transition-colors ${
              active ? 'bg-primary text-primary-foreground' : 'hover:bg-accent'
            }`}
          >
            <Icon width={18} height={18} />
            {it.label}
          </Link>
        )
      })}
    </nav>
  )
}
