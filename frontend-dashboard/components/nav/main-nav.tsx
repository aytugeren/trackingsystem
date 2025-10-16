"use client"
import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { IconExpense, IconHome, IconInvoice } from '@/components/ui/icons'

const items = [
  { href: '/', label: 'Ana Sayfa', icon: IconHome },
  { href: '/invoices', label: 'Faturalar', icon: IconInvoice },
  { href: '/expenses', label: 'Giderler', icon: IconExpense },
  { href: '/reports', label: 'Raporlar', icon: IconHome },
]

export function MainNav({ onNavigate }: { onNavigate?: () => void }) {
  const pathname = usePathname()

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
