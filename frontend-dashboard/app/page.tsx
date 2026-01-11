"use client"
import { useEffect, useState, useCallback } from 'react'
import { createPortal } from 'react-dom'
import type { Dispatch, SetStateAction } from 'react'
import { api, toDateOnlyString, type DashboardSummary, type GoldStockRow } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Separator } from '@/components/ui/separator'

export default function HomePage() {
  const [summary, setSummary] = useState<DashboardSummary | null>(null)
  const [goldStock, setGoldStock] = useState<GoldStockRow[] | null>(null)
  const [goldStockError, setGoldStockError] = useState('')
  
  const [showFullscreen, setShowFullscreen] = useState(false)
  const [filterMode, setFilterMode] = useState<'all' | 'yearly' | 'monthly' | 'daily'>('all')
  const [selectedYears, setSelectedYears] = useState<string[]>([])
  const [selectedMonths, setSelectedMonths] = useState<string[]>([])
  const [selectedDay, setSelectedDay] = useState<string>(toDateOnlyString(new Date()))
  
  const [mounted, setMounted] = useState(false)
  useEffect(() => { setMounted(true) }, [])
  useEffect(() => { try { document.body.style.overflow = showFullscreen ? 'hidden' : '' } catch {} return () => { try { document.body.style.overflow = '' } catch {} } }, [showFullscreen])

  const fetchSummary = useCallback(async () => {
    const params = {
      mode: filterMode,
      years: selectedYears,
      months: selectedMonths,
      day: filterMode === 'daily' ? selectedDay : undefined,
    }
    return api.dashboardSummary(params)
  }, [filterMode, selectedYears, selectedMonths, selectedDay])

  const fetchGoldStock = useCallback(async () => {
    return api.goldStock()
  }, [])

  useEffect(() => {
    let alive = true
    const load = async () => {
      try {
        const data = await fetchSummary()
        if (!alive) return
        setSummary(data)
      } catch {
        if (!alive) return
      }
    }
    load()
    return () => { alive = false }
  }, [fetchSummary])

  useEffect(() => {
    let alive = true
    const load = async () => {
      try {
        setGoldStockError('')
        const data = await fetchGoldStock()
        if (!alive) return
        setGoldStock(data)
      } catch {
        if (!alive) return
        setGoldStockError('Altin stok bilgisi alinamadi.')
      }
    }
    load()
    return () => { alive = false }
  }, [fetchGoldStock])
  // Auto-refresh while fullscreen overlay is open (every 10s)
  useEffect(() => {
    if (!showFullscreen) return
    let alive = true
    const fetchNow = async () => {
      try {
        const data = await fetchSummary()
        if (!alive) return
        setSummary(data)
      } catch {}
    }
    fetchNow()
    const h = setInterval(fetchNow, 10000)
    return () => { alive = false; clearInterval(h) }
  }, [showFullscreen, fetchSummary])

  
  function openFullscreen() {
    setShowFullscreen(true)
    try {
      const el: any = document.documentElement
      if (el && el.requestFullscreen && !document.fullscreenElement) {
        el.requestFullscreen().catch(() => {})
      }
    } catch {}
  }

  const availableYears = summary?.availableYears ?? []
  const availableMonths = summary?.availableMonths ?? []

  useEffect(() => {
    if (selectedYears.length === 0) return
    setSelectedMonths((prev) => prev.filter((m) => selectedYears.includes(m.slice(0, 4))))
  }, [selectedYears])

  const formatGram = (value: number) => value.toLocaleString('tr-TR', { maximumFractionDigits: 3 })
  const formatDate = (value?: string | null) => value ? new Date(value).toLocaleDateString('tr-TR') : '—'
  const karatLabelFor = (ayar: number) => (Number.isFinite(ayar) ? `${ayar} Ayar` : 'Bilinmiyor')



  return (
    <div className="space-y-8">
      <section className="rounded-xl border bg-[color:hsl(var(--card))] text-[color:hsl(var(--card-foreground))] border-[color:hsl(var(--border))] p-6 shadow-sm transition-colors duration-200">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Yönetim Paneli</h1>
            <p className="text-sm text-muted-foreground">Tüm yıllar dahil genel özet.</p>
          </div>
          <div className="flex flex-wrap items-center gap-3">
            <Button
              className="h-10 tracking-tight text-[color:hsl(var(--card-foreground))] rounded border border-[color:hsl(var(--border))] bg-[color:hsl(var(--card))]"
              variant="outline"
              onClick={openFullscreen}
            >
              Tam Ekran
            </Button>
          </div>
        </div>
        <div className="mt-5 flex flex-col gap-4 rounded-lg border border-dashed border-[color:hsl(var(--border))] p-4">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-sm text-muted-foreground">Görünüm:</span>
            <button
              className={`px-3 py-2 rounded border ${filterMode === 'all' ? 'bg-black text-white' : 'bg-[color:hsl(var(--card))] text-[color:hsl(var(--card-foreground))] border-[color:hsl(var(--border))]'}`}
              onClick={() => setFilterMode('all')}
            >
              Tüm Yıllar
            </button>
            <button
              className={`px-3 py-2 rounded border ${filterMode === 'yearly' ? 'bg-black text-white' : 'bg-[color:hsl(var(--card))] text-[color:hsl(var(--card-foreground))] border-[color:hsl(var(--border))]'}`}
              onClick={() => setFilterMode('yearly')}
            >
              Yıl Bazlı
            </button>
            <button
              className={`px-3 py-2 rounded border ${filterMode === 'monthly' ? 'bg-black text-white' : 'bg-[color:hsl(var(--card))] text-[color:hsl(var(--card-foreground))] border-[color:hsl(var(--border))]'}`}
              onClick={() => setFilterMode('monthly')}
            >
              Ay Bazlı
            </button>
            <button
              className={`px-3 py-2 rounded border ${filterMode === 'daily' ? 'bg-black text-white' : 'bg-[color:hsl(var(--card))] text-[color:hsl(var(--card-foreground))] border-[color:hsl(var(--border))]'}`}
              onClick={() => setFilterMode('daily')}
            >
              Günlük
            </button>
          </div>
          {(filterMode === 'yearly' || filterMode === 'monthly') && (
            <div className="flex flex-col gap-2">
              <div className="text-sm text-muted-foreground">Yıllar</div>
              <YearMultiSelect
                years={availableYears}
                selectedYears={selectedYears}
                setSelectedYears={setSelectedYears}
                onClear={() => setSelectedYears([])}
              />
            </div>
          )}
          {filterMode === 'monthly' && (
            <div className="flex flex-col gap-2">
              <div className="text-sm text-muted-foreground">Aylar (çoklu seçim)</div>
              <MonthMultiSelect
                months={availableMonths}
                selectedMonths={selectedMonths}
                setSelectedMonths={setSelectedMonths}
                onClear={() => setSelectedMonths([])}
              />
            </div>
          )}
          {filterMode === 'daily' && (
            <div className="flex flex-col gap-2">
              <div className="text-sm text-muted-foreground">Tarih</div>
              <input
                type="date"
                value={selectedDay}
                onChange={(e) => setSelectedDay(e.target.value)}
                className="h-10 max-w-[220px] rounded border border-[color:hsl(var(--border))] bg-[color:hsl(var(--card))] px-3 text-sm"
              />
            </div>
          )}
        </div>
      </section>

      <section>
        <h2 className="mb-3 text-sm font-medium text-muted-foreground">Bekleyen İşlemler</h2>
        <div className="grid gap-4 sm:grid-cols-2">
          {!summary ? (
            <Skeleton className="h-24 w-full" />
          ) : (
            <>
              <Card className="transition-all animate-in fade-in-50">
                <CardHeader className="py-3"><CardTitle className="text-base">Bekleyen Fatura</CardTitle></CardHeader>
                <CardContent>
                  <div className="text-2xl font-semibold tabular-nums">{(summary?.pendingInvoices ?? 0).toLocaleString('tr-TR')}</div>
                </CardContent>
              </Card>
              <Card className="transition-all animate-in fade-in-50">
                <CardHeader className="py-3"><CardTitle className="text-base">Bekleyen Gider</CardTitle></CardHeader>
                <CardContent>
                  <div className="text-2xl font-semibold tabular-nums">{(summary?.pendingExpenses ?? 0).toLocaleString('tr-TR')}</div>
                </CardContent>
              </Card>
            </>
          )}
        </div>
      </section>

      <section>
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-sm font-medium text-muted-foreground">Muhasebeye Gore Kasa Altini (gr)</h2>
          <span className="text-xs text-muted-foreground">Acilis envanteri bazli hesaplama</span>
        </div>
        {goldStockError && <p className="text-sm text-red-600 mb-2">{goldStockError}</p>}
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {!goldStock ? (
            <Skeleton className="h-32 w-full" />
          ) : goldStock.length === 0 ? (
            <p className="text-sm text-muted-foreground">Kayit bulunamadi.</p>
          ) : goldStock.map((row) => (
            <Card key={`stock-${row.karat}`} className="transition-all animate-in fade-in-50">
              <CardHeader className="py-3">
                <CardTitle className="text-base">{karatLabelFor(row.karat)}</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                <div>
                  <div className="text-sm text-muted-foreground">Acilis Envanteri (gr)</div>
                  <div className="text-2xl font-semibold tabular-nums">{formatGram(row.openingGram)}</div>
                  <div className="text-xs text-muted-foreground mt-1">{formatDate(row.openingDate)} {row.description ? `• ${row.description}` : ''}</div>
                </div>
                <Separator />
                <div>
                  <div className="text-sm text-muted-foreground">Toplam Gider Altini (gr)</div>
                  <div className="text-2xl font-semibold tabular-nums">{formatGram(row.expenseGram)}</div>
                </div>
                <Separator />
                <div>
                  <div className="text-sm text-muted-foreground">Toplam Faturalanan Altin (gr)</div>
                  <div className="text-2xl font-semibold tabular-nums">{formatGram(row.invoiceGram)}</div>
                </div>
                <Separator />
                <div>
                  <div className="text-sm text-muted-foreground">Kasa Altini (gr)</div>
                  <div className="text-2xl font-semibold tabular-nums">{formatGram(row.cashGram)}</div>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      </section>

      
      {mounted && showFullscreen && createPortal((
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
                <div className="text-6xl font-bold tabular-nums">{(summary?.pendingInvoices ?? 0).toLocaleString('tr-TR')}</div>
              </div>
              <div className="rounded-xl bg-white/10 backdrop-blur p-8 shadow-lg border border-white/20">
                <div className="text-sm text-white/70 mb-2">Bekleyen Gider</div>
                <div className="text-6xl font-bold tabular-nums">{(summary?.pendingExpenses ?? 0).toLocaleString('tr-TR')}</div>
              </div>
            </div>
            <div className="text-white/50 text-sm">Ekrana dokunarak çıkabilirsiniz • 10 sn&#39;de bir güncellenir</div>
          </div>
        </div>
      ), document.body)}

    </div>
  )
}

function YearMultiSelect({
  years,
  selectedYears,
  setSelectedYears,
  onClear,
}: {
  years: string[]
  selectedYears: string[]
  setSelectedYears: Dispatch<SetStateAction<string[]>>
  onClear: () => void
}) {
  const toggle = (y: string) => {
    setSelectedYears((prev) => (prev.includes(y) ? prev.filter((x) => x !== y) : [...prev, y]))
  }
  return (
    <div className="flex flex-wrap items-center gap-2">
      {years.length === 0 ? (
        <span className="text-sm text-muted-foreground">Yıl verisi bulunamadı.</span>
      ) : years.map((y) => {
        const active = selectedYears.includes(y)
        return (
          <button
            key={y}
            className={`px-3 py-1.5 rounded border text-sm ${active ? 'bg-black text-white' : 'bg-[color:hsl(var(--card))] text-[color:hsl(var(--card-foreground))] border-[color:hsl(var(--border))]'}`}
            onClick={() => toggle(y)}
          >
            {y}
          </button>
        )
      })}
      <button
        className="px-3 py-1.5 rounded border text-sm bg-[color:hsl(var(--card))] text-[color:hsl(var(--card-foreground))] border-[color:hsl(var(--border))]"
        onClick={onClear}
      >
        Tümünü Göster
      </button>
    </div>
  )
}

function MonthMultiSelect({
  months,
  selectedMonths,
  setSelectedMonths,
  onClear,
}: {
  months: string[]
  selectedMonths: string[]
  setSelectedMonths: Dispatch<SetStateAction<string[]>>
  onClear: () => void
}) {
  const toggle = (k: string) => {
    setSelectedMonths((prev) => (prev.includes(k) ? prev.filter((x) => x !== k) : [...prev, k]))
  }
  const labelFor = (key: string) => {
    const [y, m] = key.split('-')
    const d = new Date(Number(y), Number(m) - 1, 1)
    return d.toLocaleString('tr-TR', { month: 'long', year: 'numeric' })
  }
  return (
    <div className="flex flex-wrap items-center gap-2">
      {months.length === 0 ? (
        <span className="text-sm text-muted-foreground">Ay verisi bulunamadı.</span>
      ) : months.map((m) => {
        const active = selectedMonths.includes(m)
        return (
          <button
            key={m}
            title={labelFor(m)}
            className={`px-2 py-1 rounded border text-sm ${active ? 'bg-black text-white' : 'bg-[color:hsl(var(--card))] text-[color:hsl(var(--card-foreground))] border-[color:hsl(var(--border))]'}`}
            onClick={() => toggle(m)}
          >
            {m}
          </button>
        )
      })}
      <button
        className="px-2 py-1 rounded border text-sm bg-[color:hsl(var(--card))] text-[color:hsl(var(--card-foreground))] border-[color:hsl(var(--border))]"
        onClick={onClear}
      >
        Tümünü Göster
      </button>
    </div>
  )
}
