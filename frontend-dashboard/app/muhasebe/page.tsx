"use client"
import { useCallback, useEffect, useState } from 'react'
import { api, toDateOnlyString, type GoldOpeningInventoryRequest, type GoldStockRow } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { Separator } from '@/components/ui/separator'

export default function AccountingPage() {
  const [goldStock, setGoldStock] = useState<GoldStockRow[] | null>(null)
  const [goldStockError, setGoldStockError] = useState('')
  const [perms, setPerms] = useState<{ role?: string } | null>(null)
  const [openingForm, setOpeningForm] = useState<GoldOpeningInventoryRequest>({
    karat: 24,
    gram: 0,
    date: toDateOnlyString(new Date()),
    description: 'Muhasebe acilis bakiyesi',
  })
  const [openingSaving, setOpeningSaving] = useState(false)
  const [openingError, setOpeningError] = useState('')
  const [openingSuccess, setOpeningSuccess] = useState('')

  const fetchGoldStock = useCallback(async () => {
    return api.goldStock()
  }, [])

  useEffect(() => {
    ;(async () => {
      try {
        const base = process.env.NEXT_PUBLIC_API_BASE || ''
        const token = typeof window !== 'undefined' ? (localStorage.getItem('ktp_token') || '') : ''
        const res = await fetch(`${base}/api/me/permissions`, { cache: 'no-store', headers: token ? { Authorization: `Bearer ${token}` } : {} })
        if (res.ok) setPerms(await res.json())
      } catch {}
    })()
  }, [])

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

  const formatGram = (value: number) => value.toLocaleString('tr-TR', { maximumFractionDigits: 3 })
  const formatDate = (value?: string | null) => value ? new Date(value).toLocaleDateString('tr-TR') : '—'
  const karatLabelFor = (ayar: number) => (Number.isFinite(ayar) ? `${ayar} Ayar` : 'Bilinmiyor')

  const isAdmin = perms?.role === 'Yonetici'

  const handleOpeningSave = async () => {
    const karatValue = Number(openingForm.karat)
    const gramValue = Number(openingForm.gram)
    if (!Number.isFinite(karatValue) || karatValue <= 0) {
      setOpeningError('Karat gecersiz.')
      return
    }
    if (!openingForm.date) {
      setOpeningError('Acilis tarihi gerekli.')
      return
    }
    if (!Number.isFinite(gramValue) || gramValue < 0) {
      setOpeningError('Gram negatif olamaz.')
      return
    }

    try {
      setOpeningSaving(true)
      setOpeningError('')
      setOpeningSuccess('')
      await api.saveOpeningInventory({
        karat: karatValue,
        gram: gramValue,
        date: openingForm.date,
        description: openingForm.description?.trim() || undefined,
      })
      const stock = await fetchGoldStock()
      setGoldStock(stock)
      setOpeningSuccess('Kaydedildi.')
    } catch {
      setOpeningError('Kayit basarisiz.')
    } finally {
      setOpeningSaving(false)
    }
  }

  if (!isAdmin) {
    return <p className="text-sm text-muted-foreground">Bu sayfa icin yetkiniz yok.</p>
  }

  return (
    <div className="space-y-6">
      <section className="rounded-xl border bg-[color:hsl(var(--card))] text-[color:hsl(var(--card-foreground))] border-[color:hsl(var(--border))] p-6 shadow-sm transition-colors duration-200">
        <div className="flex flex-col gap-2">
          <h1 className="text-2xl font-semibold tracking-tight">Muhasebe</h1>
          <p className="text-sm text-muted-foreground">Acilis envanteri girisi ve karat bazli kasa altini hesaplari.</p>
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

      <section>
        <Card>
          <CardHeader className="py-3">
            <CardTitle className="text-base">Altin Acilis Envanteri Girisi</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="text-xs text-muted-foreground">Ayni karat icin kayit varsa guncellenir ve sadece acilis tarihinden sonrasi etkilenir.</div>
            {openingError && <p className="text-sm text-red-600">{openingError}</p>}
            {openingSuccess && <p className="text-sm text-emerald-600">{openingSuccess}</p>}
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <div className="flex flex-col gap-1">
                <label className="text-sm text-muted-foreground">Karat</label>
                <input
                  type="number"
                  min={1}
                  value={openingForm.karat}
                  onChange={(e) => setOpeningForm({ ...openingForm, karat: parseInt(e.target.value || '0', 10) })}
                  className="h-10 rounded border border-[color:hsl(var(--border))] bg-[color:hsl(var(--card))] px-3 text-sm"
                />
              </div>
              <div className="flex flex-col gap-1">
                <label className="text-sm text-muted-foreground">Acilis Tarihi</label>
                <input
                  type="date"
                  value={openingForm.date}
                  onChange={(e) => setOpeningForm({ ...openingForm, date: e.target.value })}
                  className="h-10 rounded border border-[color:hsl(var(--border))] bg-[color:hsl(var(--card))] px-3 text-sm"
                />
              </div>
              <div className="flex flex-col gap-1">
                <label className="text-sm text-muted-foreground">Gram</label>
                <input
                  type="number"
                  min={0}
                  step="0.001"
                  value={openingForm.gram}
                  onChange={(e) => setOpeningForm({ ...openingForm, gram: parseFloat(e.target.value || '0') })}
                  className="h-10 rounded border border-[color:hsl(var(--border))] bg-[color:hsl(var(--card))] px-3 text-sm"
                />
              </div>
              <div className="flex flex-col gap-1">
                <label className="text-sm text-muted-foreground">Aciklama</label>
                <input
                  type="text"
                  value={openingForm.description || ''}
                  onChange={(e) => setOpeningForm({ ...openingForm, description: e.target.value })}
                  className="h-10 rounded border border-[color:hsl(var(--border))] bg-[color:hsl(var(--card))] px-3 text-sm"
                  placeholder="Muhasebe acilis bakiyesi"
                />
              </div>
            </div>
            <div>
              <Button onClick={handleOpeningSave} disabled={openingSaving}>
                {openingSaving ? 'Kaydediliyor...' : 'Kaydet'}
              </Button>
            </div>
          </CardContent>
        </Card>
      </section>
    </div>
  )
}
