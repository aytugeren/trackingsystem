"use client"
import { t } from '@/lib/i18n'
import { useEffect, useMemo, useState } from 'react'
import { api, type Expense, formatDateTimeTr } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Table, TBody, TD, TH, THead, TR } from '@/components/ui/table'
import { Skeleton } from '@/components/ui/skeleton'
import { Button } from '@/components/ui/button'
import { downloadExpensesPdf, downloadExpensesXlsx } from '@/lib/export'

type Filters = {
  start?: string
  end?: string
  q: string
}

export default function ExpensesPage() {
  const r2 = (n: number) => Math.round(n * 100) / 100
  const [data, setData] = useState<Expense[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [canCancel, setCanCancel] = useState(false)
  const [canToggle, setCanToggle] = useState(false)
  const [filters, setFilters] = useState<Filters>({ q: '' })
  const [page, setPage] = useState(1)
  const [pageSize] = useState(20)
  const [totalCount, setTotalCount] = useState(0)
  const [enterTick, setEnterTick] = useState(0)
  const [showAll, setShowAll] = useState(true)
  const [nowTick, setNowTick] = useState(0)
  const [modalOpen, setModalOpen] = useState(false)
  const [selected, setSelected] = useState<Expense | null>(null)
  const [pricingAlert, setPricingAlert] = useState<string | null>(null)

  useEffect(() => {
    let mounted = true
    async function load() {
      try {
        setError(null)
        const resp = await api.listExpenses(page, pageSize)
        if (!mounted) return
        setData(resp.items)
        setTotalCount(resp.totalCount)
      } catch {
        if (!mounted) return
        setError('Veri alınamadı')
        setData(null)
      }
    }
    load()
    return () => { mounted = false }
  }, [filters.start, filters.end, filters.q, page, pageSize, enterTick])

  // Tick every second to update transient row highlights
  useEffect(() => {
    const h = setInterval(() => setNowTick(t => t + 1), 1000)
    return () => clearInterval(h)
  }, [])

  useEffect(() => {
    let mounted = true
    async function loadPricingAlert() {
      try {
        const base = process.env.NEXT_PUBLIC_API_BASE || ''
        const res = await fetch(`${base}/api/pricing/status`, { cache: 'no-store' })
        if (!mounted) return
        if (!res.ok) {
          setPricingAlert('Şu anda güncel fiyatları çekilemiyor.')
          return
        }
        const json = await res.json()
        if (json?.hasAlert) {
          setPricingAlert(json?.message || 'Şu anda güncel fiyatları çekilemiyor.')
        } else {
          setPricingAlert(null)
        }
      } catch {
        if (!mounted) return
        setPricingAlert('Şu anda güncel fiyatları çekilemiyor.')
      }
    }
    loadPricingAlert()
    const timer = setInterval(loadPricingAlert, 30000)
    return () => {
      mounted = false
      clearInterval(timer)
    }
  }, [])

  // Load minimal permissions for current user
  useEffect(() => {
    let mounted = true
    ;(async () => {
      try {
        const base = process.env.NEXT_PUBLIC_API_BASE || ''
        const token = typeof window !== 'undefined' ? (localStorage.getItem('ktp_token') || '') : ''
        const res = await fetch(`${base}/api/me/permissions`, { cache: 'no-store', headers: token ? { Authorization: `Bearer ${token}` } : {} })
        if (!mounted) return
        if (res.ok) { const j = await res.json(); setCanCancel(Boolean(j?.canCancelInvoice) || String(j?.role) === 'Yonetici'); setCanToggle(Boolean(j?.canToggleKesildi) || String(j?.role) === 'Yonetici') }
      } catch {}
    })()
    return () => { mounted = false }
  }, [])

  const filtered = useMemo(() => {
    const all = (data || []).filter(x => showAll ? true : !(x.kesildi ?? false))
    return all.filter((x) => {
      if (filters.start && x.tarih < filters.start) return false
      if (filters.end && x.tarih > filters.end) return false
      const q = filters.q.trim().toLowerCase()
      if (q) {
        const inName = (x.musteriAdSoyad || '').toLowerCase().includes(q)
        const inTckn = (x.tckn || '').toLowerCase().includes(q)
        if (!inName && !inTckn) return false
      }
      return true
    })
  }, [data, filters, showAll, nowTick])

  const total = useMemo(() => filtered.reduce((a, b) => a + Number(b.tutar), 0), [filtered])
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))
  const ay22 = selected ? ((selected as any).altinAyar === 22 || (selected as any).altinAyar === 'Ayar22') : false
  const hasAyar = Boolean(selected && (selected as any).altinAyar)
  const safOran = ay22 ? 0.916 : 0.995
  const safAltinDegeri = selected ? r2(Number(selected.altinSatisFiyati || 0) * safOran) : 0
  const gramDegeri = safAltinDegeri === 0 ? 0 : r2(Number(selected?.tutar || 0) / safAltinDegeri)
  const ayarLabel = hasAyar ? (ay22 ? '22 Ayar' : '24 Ayar') : '-'

  async function toggleStatus(exp: Expense) {
    try {
      await api.setExpenseStatus(exp.id, !(exp.kesildi ?? false))
      setData((prev) => prev ? prev.map(x => x.id === exp.id ? { ...x, kesildi: !(exp.kesildi ?? false) } : x) : prev)
      setSelected((prev) => prev && prev.id === exp.id ? { ...prev, kesildi: !(exp.kesildi ?? false) } : prev)
      try { window.dispatchEvent(new CustomEvent('ktp:tx-updated')) } catch {}
    } catch {
      setError('Durum güncellenemedi')
    }
  }
  function openFinalize(exp: Expense) {
    setSelected(exp)
    setModalOpen(true)
  }
  function closeModal() { setModalOpen(false); setSelected(null) }
  async function cancelExpense(exp: Expense) {
    if (!exp || exp.kesildi) return false
    const ok = typeof window !== 'undefined' ? window.confirm('Bu gideri iptal edip veritabanından silmek istiyor musunuz?') : true
    if (!ok) return false
    try {
      await api.deleteExpense(exp.id)
      setData(prev => prev ? prev.filter(x => x.id !== exp.id) : prev)
      return true
    } catch {
      setError('Gider silinemedi')
      return false
    }
  }

  return (
    <div className="space-y-4">
      {pricingAlert && (
        <div className="rounded border border-rose-300 bg-rose-50 px-3 py-2 text-sm font-semibold text-rose-700">
          ⚠️ {pricingAlert}
        </div>
      )}
      <Card>
        <CardHeader>
          <CardTitle>{t("filters.expense")}</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid gap-3 md:grid-cols-4">
            <div className="space-y-1">
              <Label htmlFor="start">Başlangıç Tarihi</Label>
              <Input id="start" type="date" value={filters.start || ''} onChange={(e) => setFilters((f) => ({ ...f, start: e.target.value || undefined }))} onKeyDown={(e) => { if (e.key === 'Enter') setEnterTick((t) => t + 1) }} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="end">Bitiş Tarihi</Label>
              <Input id="end" type="date" value={filters.end || ''} onChange={(e) => setFilters((f) => ({ ...f, end: e.target.value || undefined }))} onKeyDown={(e) => { if (e.key === 'Enter') setEnterTick((t) => t + 1) }} />
            </div>
            <div className="space-y-1">
              <Label htmlFor="q">Müşteri Adı / TCKN / VKN</Label>
              <Input id="q" placeholder="Ara" value={filters.q} onChange={(e) => setFilters((f) => ({ ...f, q: e.target.value }))} onKeyDown={(e) => { if (e.key === 'Enter') setEnterTick((t) => t + 1) }} />
            </div>
            <div className="flex items-end">
              <label className="inline-flex items-center gap-2 text-sm">
                <input type="checkbox" checked={!showAll} onChange={(e) => setShowAll(!e.target.checked)} />
                Sadece kesilmeyenleri göster
              </label>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card className="transition-all animate-in fade-in-50">
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>{t("list.expenses")}</CardTitle>
            <div className="flex gap-2">
              <Button variant="outline" onClick={() => downloadExpensesPdf(filtered, { start: filters.start, end: filters.end })}>PDF</Button>
              <Button variant="outline" onClick={() => downloadExpensesXlsx(filtered)}>Excel</Button>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {!data ? (
            <Skeleton className="h-32 w-full" />
          ) : (
            <>
              <Table>
                <THead>
                  <TR>
                    <TH>Tarih</TH>
                    <TH>Sıra No</TH>
                    <TH>Müşteri</TH>
                    <TH>TCKN / VKN</TH>
                    <TH>Kasiyer</TH>
                    <TH>Ayar</TH>
                    <TH>Has Altın</TH>
                    <TH className="text-right">Tutar</TH>
                    <TH>İşlem</TH>
                  </TR>
                </THead>
                <TBody>
                  {filtered.length === 0 ? (
                    <TR>
                      <TD colSpan={8} className="text-center text-sm text-muted-foreground">Kayıt bulunamadı</TD>
                    </TR>
                  ) : filtered.map((x) => {
                    const finalizedAt = (x as any).finalizedAt ? new Date((x as any).finalizedAt as any) : null
                    const recentlyFinalized = finalizedAt ? (Date.now() - finalizedAt.getTime() < 10000) : false
                    const pending = !(x.kesildi ?? false) && (x.safAltinDegeri == null)
                    const rowClass = pending
                      ? 'bg-red-600 text-white'
                      : (recentlyFinalized ? 'bg-green-600 text-white' : undefined)
                    const statusColor = x.kesildi ? 'bg-emerald-500' : 'bg-rose-500'
                    return (
                    <TR key={x.id} className={rowClass}>
                      <TD>{formatDateTimeTr(x.finalizedAt ?? x.tarih)}</TD>
                      <TD>{x.siraNo}</TD>
                      <TD>{x.isForCompany ? (x.companyName || x.musteriAdSoyad || '-') : (x.musteriAdSoyad || '-')}</TD>
                      <TD>{x.isForCompany ? (x.vknNo || '-') : (x.tckn || '-')}</TD>
                      <TD>{(x as any).kasiyerAdSoyad || '-'}</TD>
                      <TD>{(x as any).altinAyar ? (((x as any).altinAyar === 22 || (x as any).altinAyar === 'Ayar22') ? '22 Ayar' : '24 Ayar') : '-'}</TD>
                      <TD>{x.altinSatisFiyati != null ? Number(x.altinSatisFiyati).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' }) : '-'}</TD>
                      <TD className="text-right tabular-nums">{Number(x.tutar).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}</TD>
                      <TD>
                        <span className={`h-2 w-2 rounded-full ${statusColor}`}></span>
                        <Button variant="outline" size="sm" onClick={() => openFinalize(x)}>{t("btn.info.expense")}</Button>
                      </TD>
                    </TR>
                  )})}
                </TBody>
              </Table>

              {modalOpen && selected && (
                <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50">
                <div className="bg-white text-slate-900 rounded shadow p-4 w-full max-w-lg space-y-3 dark:bg-slate-900 dark:text-white">
                    <h3 className="text-lg font-semibold">{t("btn.info.expense")}</h3>
                    <div className="space-y-2 text-sm">
                      <div>Tarih: <b>{formatDateTimeTr(selected.finalizedAt ?? selected.tarih)}</b></div>
                      <div>Tutar: <b>{Number(selected.tutar).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}</b></div>
                      <div>Gram: <b>{gramDegeri.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</b></div>
                      <div>Ayar: <b>{ayarLabel}</b></div>
                      <div>İsim Soyisim: <b>{selected.isForCompany ? (selected.companyName || selected.musteriAdSoyad || '-') : (selected.musteriAdSoyad || '-')}</b></div>
                      <div>TC: <b>{selected.isForCompany ? (selected.vknNo || selected.tckn || '-') : (selected.tckn || '-')}</b></div>
                    </div>
                    <div className="flex flex-wrap items-center justify-end gap-2 pt-2">
                      <Button variant="outline" onClick={closeModal}>{t("modal.close")}</Button>
                      {canToggle && selected && (
                        <Button
                          onClick={async () => {
                            await toggleStatus(selected)
                            closeModal()
                          }}
                        >
                          {selected.kesildi ? 'Geri Al' : 'Gönder'}
                        </Button>
                      )}
                      {canCancel && selected && (
                        <Button
                          variant="default"
                          onClick={async () => {
                            const ok = await cancelExpense(selected)
                            if (ok) closeModal()
                          }}
                        >
                          İptal Et
                        </Button>
                      )}
                    </div>
                  </div>
                </div>
              )}
              <div className="flex items-center justify-between">
                <div className="space-x-2">
                  <Button variant="outline" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>Önceki</Button>
                  {Array.from({ length: Math.min(3, Math.max(1, Math.ceil(totalCount / pageSize))) }).map((_, idx) => {
                    const pNo = idx + 1
                    return <Button key={pNo} variant={pNo === page ? 'default' : 'outline'} onClick={() => setPage(pNo)}>{pNo}</Button>
                  })}
                  <Button variant="outline" disabled={page >= Math.ceil(totalCount / pageSize)} onClick={() => setPage((p) => p + 1)}>Sonraki</Button>
                </div>
                <div className="text-right font-semibold">Toplam: {total.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}</div>
              </div>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
