"use client"
import { t } from '@/lib/i18n'
import { useEffect, useMemo, useState } from 'react'
import { api, type Expense } from '@/lib/api'
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
  const [filters, setFilters] = useState<Filters>({ q: '' })
  const [page, setPage] = useState(1)
  const [pageSize] = useState(20)
  const [totalCount, setTotalCount] = useState(0)
  const [enterTick, setEnterTick] = useState(0)
  const [showAll, setShowAll] = useState(true)
  const [nowTick, setNowTick] = useState(0)
  const [modalOpen, setModalOpen] = useState(false)
  const [selected, setSelected] = useState<Expense | null>(null)
  const [urunFiyati, setUrunFiyati] = useState<string>('')

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
  }, [data, filters, showAll])

  const total = useMemo(() => filtered.reduce((a, b) => a + Number(b.tutar), 0), [filtered])
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))

  async function toggleStatus(exp: Expense) {
    try {
      await api.setExpenseStatus(exp.id, !(exp.kesildi ?? false))
      setData((prev) => (prev ? prev.map(x => x.id === exp.id ? { ...x, kesildi: !(exp.kesildi ?? false) } : x) : prev))
    } catch {
      setError('Durum güncellenemedi')
    }
  }

  function openFinalize(exp: Expense) {
    setSelected(exp)
    setUrunFiyati(String(exp.tutar))
    setModalOpen(true)
  }
  function closeModal() { setModalOpen(false); setSelected(null); setUrunFiyati('') }
  async function finalize() {
    if (!selected) return
    try {
      await api.finalizeExpense(selected.id)
      setData(prev => prev ? prev.map(x => x.id === selected.id ? { ...x, urunFiyati: x.tutar } : x) : prev)
      closeModal(); setEnterTick(t => t + 1)
    } catch { setError('Gider kesilemedi') }
  }

  async function cancelExpense(exp: Expense) {
    if (!exp || exp.kesildi) return
    const ok = typeof window !== 'undefined' ? window.confirm('Bu gideri iptal edip veritabanından silmek istiyor musunuz?') : true
    if (!ok) return
    try {
      await api.deleteExpense(exp.id)
      setData(prev => prev ? prev.filter(x => x.id !== exp.id) : prev)
    } catch { setError('Gider silinemedi') }
  }

  return (
    <div className="space-y-4">
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
              <Label htmlFor="q">Müşteri Adı / TCKN</Label>
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
                    <TH>TCKN</TH>
                    <TH>Kasiyer</TH>
                    <TH>Ayar</TH>
                    <TH>Has Altın</TH>
                    <TH>İşlem</TH>
                    <TH className="text-right">Tutar</TH>
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
                    return (
                    <TR key={x.id} className={rowClass}>
                      <TD>{x.tarih}</TD>
                      <TD>{x.siraNo}</TD>
                      <TD>{x.musteriAdSoyad || '-'}</TD>
                      <TD>{x.tckn || '-'}</TD>
                      <TD>{(x as any).kasiyerAdSoyad || '-'}</TD>
                      <TD>{(x as any).altinAyar ? (((x as any).altinAyar === 22 || (x as any).altinAyar === 'Ayar22') ? '22 Ayar' : '24 Ayar') : '-'}</TD>
                      <TD>{x.altinSatisFiyati != null ? Number(x.altinSatisFiyati).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' }) : '-'}</TD>
                      <TD className="space-x-2">
                        <button className="border rounded px-2 py-1 text-sm" onClick={() => toggleStatus(x)}>{x.kesildi ? 'Kesildi' : (pending ? 'Onay Bekliyor' : 'Bekliyor')}</button>
                        <button className="border rounded px-2 py-1 text-sm" onClick={() => openFinalize(x)}>{t("btn.info.expense")}</button>
                        {!x.kesildi && (
                          <button className="border rounded px-2 py-1 text-sm text-red-600" onClick={() => cancelExpense(x)}>İptal Et</button>
                        )}
                      </TD>
                      <TD className="text-right tabular-nums">{Number(x.tutar).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}</TD>
                    </TR>
                  )})}
                </TBody>
              </Table>

              {modalOpen && selected && (
                <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50">
                  <div className="bg-white rounded shadow p-4 w-full max-w-lg space-y-3">
                    <h3 className="text-lg font-semibold">{t("btn.info.expense")}</h3>
                    <div className="space-y-1 text-sm">
                      <div>Has Altın Fiyatı: <b>{selected.altinSatisFiyati != null ? Number(selected.altinSatisFiyati).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' }) : '-'}</b></div>
                      <div>Ayar: <b>{(selected as any).altinAyar ? (((selected as any).altinAyar === 22 || (selected as any).altinAyar === 'Ayar22') ? '22 Ayar' : '24 Ayar') : '-'}</b></div>
                      <div className="pt-2">Ürün Fiyatı</div>
                      <Input value={urunFiyati} readOnly placeholder="0,00" />
                      {(() => {
                        const r2 = (n: number) => Math.round(n * 100) / 100
                        const has = Number(selected.altinSatisFiyati || 0)
                        const ay22 = ((selected as any).altinAyar === 22 || (selected as any).altinAyar === 'Ayar22')
                        const rawSaf = has * (ay22 ? 0.916 : 0.995)
                        const u = Number(selected.tutar || 0)
                        const rawYeni = u * (ay22 ? 0.99 : 0.998)
                        const saf = r2(rawSaf)
                        const yeni = r2(rawYeni)
                        const gram = saf ? r2(yeni / saf) : 0
                        const altinHizmet = r2(gram * saf)
                        const iscilikKdvli = r2(r2(u) - altinHizmet)
                        const isc = r2(iscilikKdvli / 1.20)
                        return (
                          <div className="mt-2 space-y-1">
                            <div>Saf Altın Değeri: <b>{saf.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}</b></div>
                            <div>Yeni Ürün Fiyatı: <b>{yeni.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}</b></div>
                            <div>Gram Değeri: <b>{gram.toLocaleString('tr-TR')}</b></div>
                            <div>İşçilik (KDV’siz): <b>{isc.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}</b></div>
                          </div>
                        )
                      })()}
                    </div>
                    <div className="flex justify-end gap-2 pt-2">
                      <Button variant="outline" onClick={closeModal}>{t("modal.close")}</Button>
                      <Button onClick={() => { /* 3. parti entegrasyon için daha sonra */ }}>{'Gönder'}</Button>
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






