"use client"
import Link from 'next/link'
import { useEffect, useMemo, useState, useCallback } from 'react'
import { createPortal } from 'react-dom'
import type { Dispatch, SetStateAction } from 'react'
import { api, toDateOnlyString, type Expense, type Invoice } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Separator } from '@/components/ui/separator'

export default function HomePage() {
  const [invoices, setInvoices] = useState<Invoice[] | null>(null)
  const [expenses, setExpenses] = useState<Expense[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  
  const [showFullscreen, setShowFullscreen] = useState(false)
  const [period, setPeriod] = useState<'daily' | 'monthly' | 'yearly'>('daily')
  // Monthly multi-select support (max 3 months)
  const __now0 = new Date()
  const __defaultMonth = `${__now0.getFullYear()}-${String(__now0.getMonth() + 1).padStart(2, '0')}`
  const [selectedMonths, setSelectedMonths] = useState<string[]>([__defaultMonth])
  const [karatCfg, setKaratCfg] = useState<{ ranges: { min: number; max: number; colorHex: string }[]; alertThreshold: number } | null>(null)
  
  const [mounted, setMounted] = useState(false)
  useEffect(() => { setMounted(true) }, [])
  useEffect(() => { try { document.body.style.overflow = showFullscreen ? 'hidden' : '' } catch {} return () => { try { document.body.style.overflow = '' } catch {} } }, [showFullscreen])

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
  // Auto-refresh while fullscreen overlay is open (every 10s)
  useEffect(() => {
    if (!showFullscreen) return
    let alive = true
    const fetchNow = async () => {
      try {
        const [inv, exp] = await Promise.all([api.listAllInvoices(), api.listAllExpenses()])
        if (!alive) return
        setInvoices(inv)
        setExpenses(exp)
      } catch {}
    }
    fetchNow()
    const h = setInterval(fetchNow, 10000)
    return () => { alive = false; clearInterval(h) }
  }, [showFullscreen])

  const now = new Date()
  const todayKey = toDateOnlyString(now)
  const monthKey = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`
  const yearKey = `${now.getFullYear()}-`

  const matchByPeriod = useCallback((d: string) => {
    switch (period) {
      case 'yearly': return d.startsWith(yearKey)
      case 'monthly': return d.startsWith(monthKey)
      default: return d.startsWith(todayKey)
    }
  }, [period, todayKey, monthKey, yearKey])

    const { income, outgo, net } = useMemo(() => {
    const invs = (invoices || []).filter((x) => matchByPeriod(x.tarih))
    const exps = (expenses || []).filter((x) => matchByPeriod(x.tarih))
    const incomeSum = invs.reduce((a, b) => a + Number(b.tutar), 0)
    const outgoSum = exps.reduce((a, b) => a + Number(b.tutar), 0)
    return { income: incomeSum, outgo: outgoSum, net: incomeSum - outgoSum }
  }, [invoices, expenses, matchByPeriod])

  const detailed = useMemo(() => {
    const invs = (invoices || []).filter((x) => matchByPeriod(x.tarih))
    const exps = (expenses || []).filter((x) => matchByPeriod(x.tarih))
    const invGrams = invs.reduce((a, b) => a + Number(b.gramDegeri ?? 0), 0)
    const expGrams = exps.reduce((a, b) => a + Number(b.gramDegeri ?? 0), 0)
    const pendingInv = invs.filter((x) => !(x.kesildi ?? false)).length
    const pendingExp = exps.filter((x) => !(x.kesildi ?? false)).length
    return { invGrams, expGrams, pendingInv, pendingExp }
  }, [invoices, expenses, matchByPeriod])

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
  }, [invoices, expenses, matchByPeriod])
  
  function openFullscreen() {
    setShowFullscreen(true)
    try {
      const el: any = document.documentElement
      if (el && el.requestFullscreen && !document.fullscreenElement) {
        el.requestFullscreen().catch(() => {})
      }
    } catch {}
  }

  // Helpers for monthly multi-select filtering
  const matchByMonths = useCallback((dateStr: string) => {
    if (period !== 'monthly') return matchByPeriod(dateStr)
    const months = (selectedMonths && selectedMonths.length > 0) ? selectedMonths : [monthKey]
    return months.some((m) => dateStr.startsWith(m))
  }, [period, selectedMonths, monthKey, matchByPeriod])

  // 22/24 ayar özetleri (sadece kesilmiş işlemler)
    const karatSummary = useMemo(() => {
    const invs = (invoices || []).filter((x) => matchByMonths(x.tarih) && (x.kesildi ?? false))
    const exps = (expenses || []).filter((x) => matchByMonths(x.tarih) && (x.kesildi ?? false))
    const toAyarNum = (v: Invoice['altinAyar'] | Expense['altinAyar']): 22 | 24 | null => {
      if (v === 22 || v === 'Ayar22') return 22
      if (v === 24 || v === 'Ayar24') return 24
      return null
    }
    let inv22 = 0, inv24 = 0, exp22 = 0, exp24 = 0
    for (const i of invs) {
      const a = toAyarNum(i.altinAyar)
      if (a === 22) inv22 += Number(i.gramDegeri ?? 0)
      else if (a === 24) inv24 += Number(i.gramDegeri ?? 0)
    }
    for (const e of exps) {
      const a = toAyarNum(e.altinAyar)
      if (a === 22) exp22 += Number(e.gramDegeri ?? 0)
      else if (a === 24) exp24 += Number(e.gramDegeri ?? 0)
    }
    return { inv22, inv24, exp22, exp24 }
  }, [invoices, expenses, matchByMonths])  // Load karat settings
  useEffect(() => {
    let mounted = true
    async function loadKarat() {
      try {
        const base = process.env.NEXT_PUBLIC_API_BASE || ''
        const token = typeof window !== 'undefined' ? (localStorage.getItem('ktp_token') || '') : ''
        const res = await fetch(`${base}/api/settings/karat`, { cache: 'no-store', headers: token ? { Authorization: `Bearer ${token}` } : {} })
        if (!res.ok) throw new Error('cfg')
        const j = await res.json()
        if (!mounted) return
        setKaratCfg(j)
      } catch {
        if (!mounted) return
        setKaratCfg({
          ranges: [
            { min: 100, max: 300, colorHex: '#FFF9C4' },
            { min: 300, max: 500, colorHex: '#FFCC80' },
            { min: 500, max: 700, colorHex: '#EF9A9A' },
            { min: 700, max: 1000, colorHex: '#D32F2F' },
          ],
          alertThreshold: 1000,
        })
      }
    }
    loadKarat()
    return () => { mounted = false }
  }, [])

  const colorForDiff = (diff: number) => {
    if (!karatCfg) return undefined
    const r = karatCfg.ranges.find(r => diff >= r.min && diff < r.max)
    return r?.colorHex
  }

  const diff22 = Math.max(0, karatSummary.inv22 - karatSummary.exp22)
  const diff24 = Math.max(0, karatSummary.inv24 - karatSummary.exp24)
  const showAlert = karatCfg ? (diff22 > karatCfg.alertThreshold || diff24 > karatCfg.alertThreshold) : false

  return (
    <div className="space-y-8">
      <section className="rounded-xl border bg-[color:hsl(var(--card))] text-[color:hsl(var(--card-foreground))] border-[color:hsl(var(--border))] p-6 shadow-sm transition-colors duration-200">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Yönetim Paneli</h1>
            <p className="text-sm text-muted-foreground">Fatura ve giderlerinizi kolayca görüntüleyin.</p>
          </div>
          <div className="flex flex-col gap-3 items-end">            <div>
            <Button
              className="h-10 tracking-tight text-[color:hsl(var(--card-foreground))] rounded border border-[color:hsl(var(--border))] bg-[color:hsl(var(--card))]"
              variant="outline"
              onClick={openFullscreen}
            >
              Tam Ekran
            </Button>
            </div>
            <div className="flex items-center gap-2">
              <span className="text-sm text-muted-foreground">Dönem:</span>
              <button
                className={`px-3 py-2 rounded border ${period === 'daily' ? 'bg-black text-white' : 'bg-[color:hsl(var(--card))] text-[color:hsl(var(--card-foreground))] border-[color:hsl(var(--border))]'}`}
                onClick={() => setPeriod('daily')}
              >
                Günlük
              </button>
              <button
                className={`px-3 py-2 rounded border ${period === 'monthly' ? 'bg-black text-white' : 'bg-[color:hsl(var(--card))] text-[color:hsl(var(--card-foreground))] border-[color:hsl(var(--border))]'}`}
                onClick={() => setPeriod('monthly')}
              >
                Aylık
              </button>
              <button
                className={`px-3 py-2 rounded border ${period === 'yearly' ? 'bg-black text-white' : 'bg-[color:hsl(var(--card))] text-[color:hsl(var(--card-foreground))] border-[color:hsl(var(--border))]'}`}
                onClick={() => setPeriod('yearly')}
              >
                Yıllık
              </button>
            </div>
            <div className="grid gap-3 sm:grid-cols-2">
              <Link href="/invoices">
                <Button className="h-6 tracking-tight text-[color:hsl(var(--card-foreground))] rounded border border-[color:hsl(var(--border))] bg-[color:hsl(var(--card))]">Faturalar</Button>
              </Link>
              <Link href="/expenses">
                <Button
                  className="h-6 tracking-tight text-[color:hsl(var(--card-foreground))] rounded border border-[color:hsl(var(--border))] bg-[color:hsl(var(--card))]"
                  variant="outline"
                >
                  Giderler
                </Button>
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

      {period === 'monthly' && (
        <section>
          <h2 className="mb-3 text-sm font-medium text-muted-foreground">Ay Seçimi</h2>
          <MonthMultiSelect selectedMonths={selectedMonths} setSelectedMonths={setSelectedMonths} />
        </section>
      )}

      {period === 'monthly' && (
        <section>
          <h2 className="mb-3 text-sm font-medium text-muted-foreground">Aylık Karat Özeti (22/24 Ayar)</h2>
          <div className="grid gap-4 sm:grid-cols-4">
            <Card className="transition-all animate-in fade-in-50">
              <CardHeader className="py-3"><CardTitle className="text-base">22 Ayar - Fatura (gr)</CardTitle></CardHeader>
              <CardContent>
                <div className="text-2xl font-semibold tabular-nums">{karatSummary.inv22.toLocaleString('tr-TR', { maximumFractionDigits: 2 })}</div>
              </CardContent>
            </Card>
            <Card className="transition-all animate-in fade-in-50">
              <CardHeader className="py-3"><CardTitle className="text-base">22 Ayar - Gider (gr)</CardTitle></CardHeader>
              <CardContent>
                <div className="text-2xl font-semibold tabular-nums">{karatSummary.exp22.toLocaleString('tr-TR', { maximumFractionDigits: 2 })}</div>
              </CardContent>
            </Card>
            <Card className="transition-all animate-in fade-in-50">
              <CardHeader className="py-3"><CardTitle className="text-base">24 Ayar - Fatura (gr)</CardTitle></CardHeader>
              <CardContent>
                <div className="text-2xl font-semibold tabular-nums">{karatSummary.inv24.toLocaleString('tr-TR', { maximumFractionDigits: 2 })}</div>
              </CardContent>
            </Card>
            <Card className="transition-all animate-in fade-in-50">
              <CardHeader className="py-3"><CardTitle className="text-base">24 Ayar - Gider (gr)</CardTitle></CardHeader>
              <CardContent>
                <div className="text-2xl font-semibold tabular-nums">{karatSummary.exp24.toLocaleString('tr-TR', { maximumFractionDigits: 2 })}</div>
              </CardContent>
            </Card>
          </div>
          <div className="mt-4 grid gap-4 sm:grid-cols-2">
            <Card className="transition-all animate-in fade-in-50" style={{ backgroundColor: colorForDiff(diff22) }}>
              <CardHeader className="py-3"><CardTitle className="text-base">22 Ayar - Fark (gr)</CardTitle></CardHeader>
              <CardContent>
                <div className="text-2xl font-semibold tabular-nums">{diff22.toLocaleString('tr-TR', { maximumFractionDigits: 2 })}</div>
              </CardContent>
            </Card>
            <Card className="transition-all animate-in fade-in-50" style={{ backgroundColor: colorForDiff(diff24) }}>
              <CardHeader className="py-3"><CardTitle className="text-base">24 Ayar - Fark (gr)</CardTitle></CardHeader>
              <CardContent>
                <div className="text-2xl font-semibold tabular-nums">{diff24.toLocaleString('tr-TR', { maximumFractionDigits: 2 })}</div>
              </CardContent>
            </Card>
          </div>
        </section>
      )}

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
      </section>      {mounted && showFullscreen && createPortal((
        <div
          className="fixed inset-0 z-[100] flex flex-col items-center justify-center bg-black text-white"
          role="dialog"
          aria-modal="true"
          onClick={() => setShowFullscreen(false)}
        >
          <div className="absolute top-6 right-6">
            <Button variant="outline" className="bg-white text-black" onClick={(e) => { e.stopPropagation(); setShowFullscreen(false); try { if (document.fullscreenElement) document.exitFullscreen().catch(()=>{}) } catch {} }}>Kapat</Button>
          </div>
          <div className="relative flex flex-col items-center gap-10 px-6 text-center" onClick={(e) => e.stopPropagation()}>
            <div className="flex items-center gap-4">
              <span className="text-6xl animate-pulse">💎</span>
              <h2 className="text-4xl font-extrabold tracking-tight">Eren Kuyumculuk</h2>
            </div>
            <div className="grid gap-8 sm:grid-cols-2 w-full max-w-4xl">
              <div className="rounded-xl bg-white/10 backdrop-blur p-8 shadow-lg border border-white/20">
                <div className="text-sm text-white/70 mb-2">Bekleyen Fatura</div>
                <div className="text-6xl font-bold tabular-nums">{(invoices || []).filter(x => !(x.kesildi ?? false)).length.toLocaleString('tr-TR')}</div>
              </div>
              <div className="rounded-xl bg-white/10 backdrop-blur p-8 shadow-lg border border-white/20">
                <div className="text-sm text-white/70 mb-2">Bekleyen Gider</div>
                <div className="text-6xl font-bold tabular-nums">{(expenses || []).filter(x => !(x.kesildi ?? false)).length.toLocaleString('tr-TR')}</div>
              </div>
            </div>
            <div className="text-white/50 text-sm">Ekrana dokunarak çıkabilirsiniz • 10 sn&#39;de bir güncellenir</div>
          </div>
        </div>
      ), document.body)}

    </div>
  )
}

function MonthMultiSelect({ selectedMonths, setSelectedMonths }: { selectedMonths: string[]; setSelectedMonths: Dispatch<SetStateAction<string[]>> }) {
  // Build last 12 months including current
  const items: { key: string; label: string }[] = []
  const now = new Date()
  for (let i = 0; i < 12; i++) {
    const d = new Date(now.getFullYear(), now.getMonth() - i, 1)
    const key = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`
    const label = d.toLocaleString('tr-TR', { month: 'long', year: 'numeric' })
    items.push({ key, label })
  }
  const toggle = (k: string) => {
    setSelectedMonths((prev) => {
      const has = prev.includes(k)
      if (has) return prev.filter((x) => x !== k)
      if (prev.length >= 3) return prev // max 3
      return [...prev, k]
    })
  }
  const isDisabled = (k: string) => !selectedMonths.includes(k) && selectedMonths.length >= 3
  return (
    <div className="flex flex-wrap items-center gap-2">
      {items.map((m) => {
        const active = selectedMonths.includes(m.key)
        return (
          <button
            key={m.key}
            title={m.label}
            className={`px-2 py-1 rounded border text-sm ${active ? 'bg-black text-white' : 'bg-[color:hsl(var(--card))] text-[color:hsl(var(--card-foreground))] border-[color:hsl(var(--border))]'} ${isDisabled(m.key) ? 'opacity-50 cursor-not-allowed' : ''}`}
            onClick={() => !isDisabled(m.key) && toggle(m.key)}
          >
            {m.key}
          </button>
        )
      })}
      <span className="text-xs text-muted-foreground">(En fazla 3 ay seçilebilir)</span>
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














