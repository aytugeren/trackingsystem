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
  const [period, setPeriod] = useState<'daily' | 'monthly' | 'yearly'>('daily')

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
    return () => { mounted = false }
  }, [])

  const now = new Date()
  const todayKey = toDateOnlyString(now)
  const monthKey = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`
  const yearKey = `${now.getFullYear()}-`

  const matchByPeriod = (d: string) => {
    switch (period) {
      case 'yearly': return d.startsWith(yearKey)
      case 'monthly': return d.startsWith(monthKey)
      default: return d.startsWith(todayKey)
    }
  }

  const { income, outgo, net } = useMemo(() => {
    const invs = (invoices || []).filter((x) => matchByPeriod(x.tarih))
    const exps = (expenses || []).filter((x) => matchByPeriod(x.tarih))
    const incomeSum = invs.reduce((a, b) => a + Number(b.tutar), 0)
    const outgoSum = exps.reduce((a, b) => a + Number(b.tutar), 0)
    return { income: incomeSum, outgo: outgoSum, net: incomeSum - outgoSum }
  }, [invoices, expenses, period, todayKey, monthKey, yearKey])

  const detailed = useMemo(() => {
    const invs = (invoices || []).filter((x) => matchByPeriod(x.tarih))
    const exps = (expenses || []).filter((x) => matchByPeriod(x.tarih))
    const invGrams = invs.reduce((a, b) => a + Number(b.gramDegeri ?? 0), 0)
    const expGrams = exps.reduce((a, b) => a + Number(b.gramDegeri ?? 0), 0)
    const pendingInv = invs.filter((x) => !(x.kesildi ?? false)).length
    const pendingExp = exps.filter((x) => !(x.kesildi ?? false)).length
    return { invGrams, expGrams, pendingInv, pendingExp }
  }, [invoices, expenses, period, todayKey, monthKey, yearKey])

  const cashierStats = useMemo(() => {
    const invs = (invoices || []).filter((x) => matchByPeriod(x.tarih) && (x.kesildi ?? false))
    const exps = (expenses || []).filter((x) => matchByPeriod(x.tarih) && (x.kesildi ?? false))
    const map = new Map<string, { invoice: number; expense: number }>()
    for (const inv of invs) {
      const key = (inv.kasiyerAdSoyad || 'Bilinmiyor') as string
      const cur = map.get(key) || { invoice: 0, expense: 0 }
      cur.invoice += 1
      map.set(key, cur)
    }
    for (const exp of exps) {
      const key = (exp.kasiyerAdSoyad || 'Bilinmiyor') as string
      const cur = map.get(key) || { invoice: 0, expense: 0 }
      cur.expense += 1
      map.set(key, cur)
    }
    return Array.from(map.entries()).sort((a, b) => (b[1].invoice + b[1].expense) - (a[1].invoice + a[1].expense))
  }, [invoices, expenses, period, todayKey, monthKey, yearKey])

  return (
    <div className="space-y-8">
      <section className="rounded-xl border bg-white p-6 shadow-sm">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Yönetim Paneli</h1>
            <p className="text-sm text-muted-foreground">Fatura ve giderlerinizi kolayca görüntüleyin.</p>
          </div>
          <div className="flex flex-col gap-3 items-end">
            <div className="flex items-center gap-2">
              <span className="text-sm text-muted-foreground">Dönem:</span>
              <button className={`px-3 py-2 rounded border ${period==='daily'?'bg-black text-white':'bg-white'}`} onClick={() => setPeriod('daily')}>Günlük</button>
              <button className={`px-3 py-2 rounded border ${period==='monthly'?'bg-black text-white':'bg-white'}`} onClick={() => setPeriod('monthly')}>Aylık</button>
              <button className={`px-3 py-2 rounded border ${period==='yearly'?'bg-black text-white':'bg-white'}`} onClick={() => setPeriod('yearly')}>Yıllık</button>
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
        </div>
      </section>

      <section>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Seçili Dönem Özeti</h2>
        <div className="grid gap-4 sm:grid-cols-3">
          <SummaryCard title="Toplam Gelir" emoji="💰" value={invoices ? income : null} error={error} />
          <SummaryCard title="Toplam Gider" emoji="💸" value={expenses ? outgo : null} error={error} />
          <SummaryCard title="Net Kazanç" emoji="📈" value={invoices && expenses ? net : null} error={error} />
        </div>
      </section>

      <section>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Detaylı Özet</h2>
        <div className="grid gap-4 sm:grid-cols-4">
          <Card className="transition-all animate-in fade-in-50">
            <CardHeader className="py-3"><CardTitle className="text-base">Faturalanan Altın (gr)</CardTitle></CardHeader>
            <CardContent>
              <div className="text-2xl font-semibold tabular-nums">{detailed.invGrams.toLocaleString('tr-TR', { maximumFractionDigits: 2 })}</div>
            </CardContent>
          </Card>
          <Card className="transition-all animate-in fade-in-50">
            <CardHeader className="py-3"><CardTitle className="text-base">Gider Altını (gr)</CardTitle></CardHeader>
            <CardContent>
              <div className="text-2xl font-semibold tabular-nums">{detailed.expGrams.toLocaleString('tr-TR', { maximumFractionDigits: 2 })}</div>
            </CardContent>
          </Card>
          <Card className="transition-all animate-in fade-in-50">
            <CardHeader className="py-3"><CardTitle className="text-base">Bekleyen Fatura</CardTitle></CardHeader>
            <CardContent>
              <div className="text-2xl font-semibold tabular-nums">{detailed.pendingInv.toLocaleString('tr-TR')}</div>
            </CardContent>
          </Card>
          <Card className="transition-all animate-in fade-in-50">
            <CardHeader className="py-3"><CardTitle className="text-base">Bekleyen Gider</CardTitle></CardHeader>
            <CardContent>
              <div className="text-2xl font-semibold tabular-nums">{detailed.pendingExp.toLocaleString('tr-TR')}</div>
            </CardContent>
          </Card>
        </div>
      </section>

      <section>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Kasiyer Bazlı İstatistikler</h2>
        <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
          {(!invoices && !expenses) ? (
            <Skeleton className="h-32 w-full" />
          ) : cashierStats.length === 0 ? (
            <p className="text-sm text-muted-foreground">Kayıt bulunamadı.</p>
          ) : cashierStats.map(([name, s]) => (
            <Card key={name} className="transition-all animate-in fade-in-50">
              <CardHeader className="py-3">
                <CardTitle className="text-base">{name}</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="text-sm text-muted-foreground">Fatura</div>
                <div className="text-2xl font-semibold tabular-nums">{s.invoice}</div>
                <Separator className="my-2" />
                <div className="text-sm text-muted-foreground">Gider</div>
                <div className="text-2xl font-semibold tabular-nums">{s.expense}</div>
              </CardContent>
            </Card>
          ))}
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

