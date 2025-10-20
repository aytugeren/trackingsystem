"use client"
import { useEffect, useMemo, useState } from 'react'
import { api, type Invoice } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select } from '@/components/ui/select'
import { Table, TBody, TD, TH, THead, TR } from '@/components/ui/table'
import { Skeleton } from '@/components/ui/skeleton'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { downloadInvoicesPdf, downloadInvoicesXlsx } from '@/lib/export'

type Filters = {
  start?: string
  end?: string
  method: 'all' | 'havale' | 'kredikarti'
  q: string
}

export default function InvoicesPage() {
  const r2 = (n: number) => Math.round(n * 100) / 100;
  const [data, setData] = useState<Invoice[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [filters, setFilters] = useState<Filters>({ method: 'all', q: '' })
  const [page, setPage] = useState(1)
  const [pageSize] = useState(20)
  const [totalCount, setTotalCount] = useState(0)
  const [enterTick, setEnterTick] = useState(0)
  const [modalOpen, setModalOpen] = useState(false)
  const [selected, setSelected] = useState<Invoice | null>(null)

  // Load from API (pagination + lazy load)
  useEffect(() => {
    let mounted = true
    async function load() {
      try {
        setError(null)
        const resp = await api.listInvoices(page, pageSize)
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
  }, [filters.start, filters.end, filters.method, filters.q, page, pageSize, enterTick])

  const filtered = useMemo(() => {
    const all = data || []
    return all.filter((x) => {
      const isHavale = (x.odemeSekli === 0 || (x.odemeSekli as any) === 'Havale')
      if (filters.start && x.tarih < filters.start) return false
      if (filters.end && x.tarih > filters.end) return false
      if (filters.method === 'havale' && !isHavale) return false
      if (filters.method === 'kredikarti' && isHavale) return false
      const q = filters.q.trim().toLowerCase()
      if (q) {
        const inName = (x.musteriAdSoyad || '').toLowerCase().includes(q)
        const inTckn = (x.tckn || '').toLowerCase().includes(q)
        if (!inName && !inTckn) return false
      }
      return true
    })
  }, [data, filters])

  const total = useMemo(() => filtered.reduce((a, b) => a + Number(b.tutar), 0), [filtered])

  async function toggleStatus(inv: Invoice) {
    try {
      await api.setInvoiceStatus(inv.id, !(inv.kesildi ?? false))
      setData((prev) => (prev ? prev.map(x => x.id === inv.id ? { ...x, kesildi: !(inv.kesildi ?? false) } : x) : prev))
    } catch {
      setError('Durum güncellenemedi')
    }
  }

  function openFinalize(inv: Invoice) {
    setSelected(inv)
    setModalOpen(true)
  }

  function closeModal() {
    setModalOpen(false)
    setSelected(null)
  }

  async function finalize() {
    if (!selected) return
    try {
      await api.finalizeInvoice(selected.id)
      // refetch page or optimistically mark as kesildi
      setData(prev => prev ? prev.map(x => x.id === selected.id ? { ...x, kesildi: true, urunFiyati: x.tutar } : x) : prev)
      closeModal()
      setEnterTick(t => t + 1) // trigger reload to get computed fields
    } catch {
      setError('Fatura kesilemedi')
    }
  }

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader>
          <CardTitle>Fatura Filtreleri</CardTitle>
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
              <Label htmlFor="method">Ödeme Şekli</Label>
              <Select id="method" value={filters.method} onChange={(e) => setFilters((f) => ({ ...f, method: e.target.value as Filters['method'] }))}>
                <option value="all">Tümü</option>
                <option value="havale">Havale</option>
                <option value="kredikarti">Kredi Kartı</option>
              </Select>
            </div>
            <div className="space-y-1">
              <Label htmlFor="q">Müşteri Adı / TCKN</Label>
              <Input id="q" placeholder="Ara" value={filters.q} onChange={(e) => setFilters((f) => ({ ...f, q: e.target.value }))} onKeyDown={(e) => { if (e.key === 'Enter') setEnterTick((t) => t + 1) }} />
            </div>
          </div>
        </CardContent>
      </Card>

      <Card className="transition-all animate-in fade-in-50">
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle>Faturalar</CardTitle>
            <div className="flex gap-2">
              <Button variant="outline" onClick={() => downloadInvoicesPdf(filtered, { start: filters.start, end: filters.end })}>PDF olarak indir</Button>
              <Button variant="outline" onClick={() => downloadInvoicesXlsx(filtered)}>Excel olarak indir</Button>
            </div>
          </div>
        </CardHeader>
        <CardContent className="space-y-3">
          {error ? (
            <p className="text-sm text-red-600">{error}</p>
          ) : !data ? (
            <div className="space-y-2">
              <Skeleton className="h-6 w-1/3" />
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-full" />
              <Skeleton className="h-10 w-2/3" />
            </div>
          ) : (
            <>
              <Table>
                <THead>
                  <TR>
                    <TH>Tarih</TH>
                    <TH>Sıra No</TH>
                    <TH>Müşteri Ad Soyad</TH>
                    <TH>TCKN</TH>
                    <TH>Kasiyer</TH>
                    <TH>Ayar</TH>
                    <TH>Has Altın</TH>
                    <TH className="text-right">Tutar</TH>
                    <TH>Durum</TH>
                    <TH>Ödeme Şekli</TH>
                  </TR>
                </THead>
                <TBody>
                  {filtered.length === 0 ? (
                    <TR>
                      <TD colSpan={6} className="text-center text-sm text-muted-foreground">Kayıt bulunamadı</TD>
                    </TR>
                  ) : filtered.map((x) => (
                  <TR key={x.id}>
                    <TD>{x.tarih}</TD>
                    <TD>{x.siraNo}</TD>
                    <TD>{x.musteriAdSoyad || '-'}</TD>
                    <TD>{x.tckn || '-'}</TD>
                    <TD>{(x as any).kasiyerAdSoyad || '-'}</TD>
                    <TD>{(x as any).altinAyar ? (((x as any).altinAyar === 22 || (x as any).altinAyar === 'Ayar22') ? '22 Ayar' : '24 Ayar') : '-'}</TD>
                    <TD>{x.altinSatisFiyati != null ? Number(x.altinSatisFiyati).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' }) : '-'}</TD>
                    <TD className="text-right tabular-nums">{Number(x.tutar).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}</TD>
                    <TD className="space-x-2">
                      <button className="border rounded px-2 py-1 text-sm" onClick={() => toggleStatus(x)}>{x.kesildi ? 'Kesildi' : 'Bekliyor'}</button>
                      <button className="border rounded px-2 py-1 text-sm" onClick={() => openFinalize(x)}>Fatura Kes</button>
                    </TD>
                    <TD>
                        {(x.odemeSekli === 0 || (x.odemeSekli as any) === 'Havale') ? (
                          <Badge variant="success">Havale</Badge>
                        ) : (
                          <Badge variant="warning">Kredi Kartı</Badge>
                        )}
                    </TD>
                    </TR>
                  ))}
                </TBody>
              </Table>
              {modalOpen && selected && (
                <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50">
                  <div className="bg-white rounded shadow p-4 w-full max-w-lg space-y-3">
                    <h3 className="text-lg font-semibold">Fatura Kes</h3>
                    <div className="space-y-1 text-sm">
                      <div>Has Altın Fiyatı: <b>{selected.altinSatisFiyati != null ? Number(selected.altinSatisFiyati).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' }) : '-'}</b></div>
                      <div>Ayar: <b>{(selected as any).altinAyar ? (((selected as any).altinAyar === 22 || (selected as any).altinAyar === 'Ayar22') ? '22 Ayar' : '24 Ayar') : '-'}</b></div>
                      <div className="pt-2">Ürün Fiyatı</div>
                      <Input value={String(selected.tutar || 0)} readOnly placeholder="0,00" />
                      {/* Client-side preview using Tutar */}
                      {(() => {
                        const has = Number(selected.altinSatisFiyati || 0)
                        const ay22 = ((selected as any).altinAyar === 22 || (selected as any).altinAyar === 'Ayar22')
                        const saf = has * (ay22 ? 0.916 : 0.995)
                        const u = Number(selected.tutar || 0)
                        const yeni = u * (ay22 ? 0.99 : 0.998)
                        const gram = saf ? (yeni / saf) : 0
                        const isc = (u - (gram * saf)) / 1.20
                        return (
                          <div className="mt-2 space-y-1">
                            <div>Saf Altın Değeri: <b>{saf.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}</b></div>
                            <div>Yeni Ürün Fiyatı: <b>{yeni.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}</b></div>
                            <div>Gram Değeri: <b>{gram.toLocaleString('tr-TR')}</b></div>
                            <div>İşçilik: <b>{isc.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}</b></div>
                          </div>
                        )
                      })()}
                    </div>
                    <div className="flex justify-end gap-2 pt-2">
                      <Button variant="outline" onClick={closeModal}>Vazgeç</Button>
                      <Button onClick={finalize}>Kes</Button>
                    </div>
                  </div>
                </div>
              )}
              <div className="flex items-center justify-between">
                <div className="space-x-2">
                  <Button variant="outline" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>← Önceki</Button>
                  {Array.from({ length: Math.min(3, Math.max(1, Math.ceil(totalCount / pageSize))) }).map((_, idx) => {
                    const pNo = idx + 1
                    return <Button key={pNo} variant={pNo === page ? 'default' : 'outline'} onClick={() => setPage(pNo)}>{pNo}</Button>
                  })}
                  <Button variant="outline" disabled={page >= Math.ceil(totalCount / pageSize)} onClick={() => setPage((p) => p + 1)}>Sonraki →</Button>
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






