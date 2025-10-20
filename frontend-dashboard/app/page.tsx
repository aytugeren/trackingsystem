"use client"
import Link from 'next/link'
import { useEffect, useMemo, useState } from 'react'
import { api, toDateOnlyString, type Expense, type Invoice } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Separator } from '@/components/ui/separator'

export default function HomePage() {
  const [invoices, setInvoices] = useState<Invoice[] | null>(null)
  const [expenses, setExpenses] = useState<Expense[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let mounted = true
    async function load() {
      try {
        setError(null)
        const [inv, exp] = await Promise.all([api.listAllInvoices(), api.listAllExpenses()])
        if (!mounted) return
        setInvoices(inv)
        setExpenses(exp)
      } catch {
        if (!mounted) return
        setError('Veri alınamadı')
      }
    }
    load()
    return () => {
      mounted = false
    }
  }, [])

  const todayKey = toDateOnlyString(new Date())

  const { income, outgo, net } = useMemo(() => {
    const dayInvoices = (invoices || []).filter((x) => x.tarih.startsWith(todayKey))
    const dayExpenses = (expenses || []).filter((x) => x.tarih.startsWith(todayKey))
    const incomeSum = dayInvoices.reduce((a, b) => a + Number(b.tutar), 0)
    const outgoSum = dayExpenses.reduce((a, b) => a + Number(b.tutar), 0)
    return { income: incomeSum, outgo: outgoSum, net: incomeSum - outgoSum }
  }, [invoices, expenses, todayKey])

  return (
    <div className="space-y-8">
      <section className="rounded-xl border bg-white p-6 shadow-sm">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Yönetim Paneli</h1>
            <p className="text-sm text-muted-foreground">Fatura ve giderlerinizi kolayca görüntüleyin.</p>
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            <Link href="/invoices">
              <Button className="w-full h-12 text-base">Faturalar</Button>
            </Link>
            <Link href="/expenses">
              <Button className="w-full h-12 text-base" variant="outline">Giderler</Button>
            </Link>
          </div>
        </div>
      </section>

      <section>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Bugünün Özeti</h2>
        <div className="grid gap-4 sm:grid-cols-3">
          <SummaryCard title="Günlük Toplam Gelir" emoji="💰" value={invoices ? income : null} error={error} />
          <SummaryCard title="Günlük Toplam Gider" emoji="💸" value={expenses ? outgo : null} error={error} />
          <SummaryCard title="Günlük Net Kazanç" emoji="📈" value={invoices && expenses ? net : null} error={error} />
        </div>
      </section>
    </div>
  )
}

function SummaryCard({ title, value, error, emoji }: { title: string; value: number | null; error: string | null; emoji: string }) {
  return (
    <Card className="transition-all animate-in fade-in-50">
      <CardHeader className="flex-row items-center justify-between">
        <CardTitle className="text-base flex items-center gap-2">
          <span className="text-xl">{emoji}</span>
          {title}
        </CardTitle>
      </CardHeader>
      <CardContent>
        {error ? (
          <p className="text-sm text-red-600">{error}</p>
        ) : value === null ? (
          <Skeleton className="h-8 w-40" />
        ) : (
          <div className="flex items-center justify-between">
            <span className="text-sm text-muted-foreground">Toplam</span>
            <p className="text-2xl font-semibold tabular-nums">{value.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}</p>
          </div>
        )}
      </CardContent>
    </Card>
  )
}

