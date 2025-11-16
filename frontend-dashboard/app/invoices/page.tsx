"use client"
import { t } from '@/lib/i18n'
import { useEffect, useMemo, useState } from 'react'
import { api, type Invoice, formatDateTimeTr } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select } from '@/components/ui/select'
import { Table, TBody, TD, TH, THead, TR } from '@/components/ui/table'
import { Skeleton } from '@/components/ui/skeleton'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { downloadInvoicesPdf, downloadInvoicesXlsx } from '@/lib/export'
import { IconCopy, IconCheck } from '@/components/ui/icons'

type Filters = {
  start?: string
  end?: string
  method: 'all' | 'havale' | 'kredikarti'
  q: string
}

export default function InvoicesPage() {
  const r2 = (n: number) => Math.round(n * 100) / 100
  const [data, setData] = useState<Invoice[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [canCancel, setCanCancel] = useState(false)
  const [canToggle, setCanToggle] = useState(false)
  const [filters, setFilters] = useState<Filters>({ method: 'all', q: '' })
  const [page, setPage] = useState(1)
  const [pageSize] = useState(20)
  const [totalCount, setTotalCount] = useState(0)
  const [enterTick, setEnterTick] = useState(0)
  const [showAll, setShowAll] = useState(true)
  const [nowTick, setNowTick] = useState(0)
  const [modalOpen, setModalOpen] = useState(false)
  const [selected, setSelected] = useState<Invoice | null>(null)
  const [copied, setCopied] = useState<string | null>(null)
  const [pricingAlert, setPricingAlert] = useState<string | null>(null)
  async function copy(key: string, text: string) {
    try {
      const s = (text ?? '').toString().trim()
      const forClipboard = /^-?\d+\.\d+$/.test(s) ? s.replace(/\./g, ',') : s
      await navigator.clipboard.writeText(forClipboard)
      setCopied(key)
      setTimeout(() => setCopied(null), 1500)
    } catch {}
  }
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

  // Tick every second to update transient row highlights
  useEffect(() => {
    const h = setInterval(() => setNowTick(t => t + 1), 1000)
    return () => clearInterval(h)
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

  const filtered = useMemo(() => {
    const all = (data || []).filter(x => showAll ? true : !(x.kesildi ?? false))
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
  }, [data, filters, showAll])

  const total = useMemo(() => filtered.reduce((a, b) => a + Number(b.tutar), 0), [filtered])

  async function toggleStatus(inv: Invoice) {
    try {
      await api.setInvoiceStatus(inv.id, !(inv.kesildi ?? false))
      setData((prev) => prev ? prev.map(x => x.id === inv.id ? { ...x, kesildi: !(inv.kesildi ?? false) } : x) : prev)
      setSelected((prev) => prev && prev.id === inv.id ? { ...prev, kesildi: !(inv.kesildi ?? false) } : prev)
      try { window.dispatchEvent(new CustomEvent('ktp:tx-updated')) } catch {}
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

  async function cancelInvoice(inv: Invoice) {
    if (!inv || inv.kesildi) return false
    const ok = typeof window !== 'undefined' ? window.confirm('Bu faturayı iptal edip veritabanından silmek istiyor musunuz?') : true
    if (!ok) return false
    try {
      await api.deleteInvoice(inv.id)
      setData(prev => prev ? prev.filter(x => x.id !== inv.id) : prev)
      return true
    } catch {
      setError('Fatura silinemedi')
      return false
    }
  }

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))

  return (
    <div className="space-y-4">
      {pricingAlert && (
        <div className="rounded border border-rose-300 bg-rose-50 px-3 py-2 text-sm font-semibold text-rose-700">
          ⚠️ {pricingAlert}
        </div>
      )}
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
            <CardTitle>Faturalar</CardTitle>
            <div className="flex gap-2">
              <Button variant="outline" onClick={() => downloadInvoicesPdf(filtered, { start: filters.start, end: filters.end })}>PDF</Button>
              <Button variant="outline" onClick={() => downloadInvoicesXlsx(filtered)}>Excel</Button>
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
                    <TH className="text-right">Tutar</TH>
                    <TH>İşlem</TH>
                    <TH>Ödeme Şekli</TH>
                  </TR>
                </THead>
                <TBody>
                  {filtered.length === 0 ? (
                    <TR>
                      <TD colSpan={10} className="text-center text-sm text-muted-foreground">Kayıt bulunamadı</TD>
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
                      <TD>{x.musteriAdSoyad || '-'}</TD>
                      <TD>{x.tckn || '-'}</TD>
                      <TD>{(x as any).kasiyerAdSoyad || '-'}</TD>
                      <TD>{(x as any).altinAyar ? (((x as any).altinAyar === 22 || (x as any).altinAyar === 'Ayar22') ? '22 Ayar' : '24 Ayar') : '-'}</TD>
                      <TD>{x.altinSatisFiyati != null ? Number(x.altinSatisFiyati).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' }) : '-'}</TD>
                      <TD className="text-right tabular-nums">{Number(x.tutar).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}</TD>
                      <TD>
                        <Button variant="outline" size="sm" onClick={() => openFinalize(x)}>Fatura Bilgileri</Button>
                      </TD>
                      <TD>
                        {(x.odemeSekli === 0 || (x.odemeSekli as any) === 'Havale') ? (
                          <Badge variant="success">Havale</Badge>
                        ) : (
                          <Badge variant="warning">Kredi Kartı</Badge>
                        )}
                      </TD>
                    </TR>
                  )})}
                </TBody>
              </Table>

              {modalOpen && selected && (
                <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50">
                <div className="bg-white text-slate-900 rounded shadow p-4 w-full max-w-lg space-y-3 dark:bg-slate-900 dark:text-white">
                    <h3 className="text-lg font-semibold">Fatura Bilgileri</h3>
                    <div className="space-y-2 text-sm">
                      <div className="flex items-center justify-between gap-2">
                        <div>İsim Soyisim: <b>{selected.musteriAdSoyad || '-'}</b></div>
                        {selected.musteriAdSoyad ? (
                          <button className="inline-flex items-center justify-center align-middle p-1 leading-none" title={copied === 'musteri' ? 'Kopyalandı' : 'Kopyala'} aria-label="Kopyala" onClick={() => copy('musteri', String(selected.musteriAdSoyad))}>
                            {copied === 'musteri' ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}
                          </button>
                        ) : null}
                      </div>
                      <div className="flex items-center justify-between gap-2">
                        <div>T.C. Kimlik No: <b>{selected.tckn || '-'}</b></div>
                        {selected.tckn ? (
                          <button className="inline-flex items-center justify-center align-middle p-1 leading-none" title={copied === 'tckn' ? 'Kopyalandı' : 'Kopyala'} aria-label="Kopyala" onClick={() => copy('tckn', String(selected.tckn))}>
                            {copied === 'tckn' ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}
                          </button>
                        ) : null}
                      </div>
                      <div>Has Altın Fiyatı: <b>{selected.altinSatisFiyati != null ? Number(selected.altinSatisFiyati).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' }) : '-'}</b></div>
                      <div>Ayar: <b>{(selected as any).altinAyar ? (((selected as any).altinAyar === 22 || (selected as any).altinAyar === 'Ayar22') ? '22 Ayar' : '24 Ayar') : '-'}</b></div>
                      <div className="flex items-center justify-between gap-2">
                        <div>Ürün Fiyatı: <b>{selected.tutar.toLocaleString("tr-TR", { style: "currency", currency: "TRY" })}</b></div>
                        {selected.tutar ? (
                          <button className="inline-flex items-center justify-center align-middle p-1 leading-none" title={copied === 'tutar' ? 'Kopyalandı' : 'Kopyala'} aria-label="Kopyala" onClick={() => copy('tutar', String(selected.tutar || 0))}>
                            {copied === 'tutar' ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}
                          </button>
                        ) : null}
                      </div>
                      {(() => {
                        const r2 = (n: number) => Math.round(n * 100) / 100;

                        const has = Number(selected.altinSatisFiyati || 0);
                        const ay22 =
                          (selected as any).altinAyar === 22 ||
                          (selected as any).altinAyar === "Ayar22";

                        // 22 ayar için saf oran 0.916, 24 ayar için 0.995
                        const safOran = ay22 ? 0.916 : 0.995;
                        const yeniOran = ay22 ? 0.99 : 0.998;

                        const rawSaf = has * safOran; // Saf altın fiyatı
                        const u = Number(selected.tutar || 0); // Yeni ürün fiyatı
                        const rawYeni = u * yeniOran; // Yeni ürünün altın karşılığı

                        const saf = r2(rawSaf);
                        const yeni = r2(rawYeni);

                        // Gram = yeni ürün değeri / saf altın fiyatı
                        const gram = saf > 0 ? r2(yeni / saf) : 0;

                        // Altın hizmet bedeli = saf * gram (değer değil oranla çarpım)
                        const altinHizmet = r2(saf * gram);

                        // İşçilik KDV dâhil
                        const iscilikKdvli = r2(u - altinHizmet);

                        // KDV’siz işçilik
                        const isc = r2(iscilikKdvli / 1.20);

                        return (
                          <div className="mt-2 space-y-1">
                            <div className="flex items-center justify-between gap-2">
                              <div>
                                Saf Altın Değeri: <b>{saf.toLocaleString("tr-TR", { style: "currency", currency: "TRY" })}</b>
                              </div>
                              <button
                                className="inline-flex items-center justify-center align-middle p-1 leading-none"
                                title={copied === "saf" ? "Kopyalandı" : "Saf Altın Değeri kopyala"}
                                aria-label="Saf Altın Değeri"
                                onClick={() => copy("saf", String(saf))}
                              >
                                {copied === "saf" ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}
                              </button>
                            </div>
                            <div className="flex items-center justify-between gap-2">
                              <div>
                                Yeni Ürün Fiyatı: <b>{yeni.toLocaleString("tr-TR", { style: "currency", currency: "TRY" })}</b>
                              </div>
                              <button
                                className="inline-flex items-center justify-center align-middle p-1 leading-none"
                                title={copied === "yeni" ? "Kopyalandı" : "Yeni Ürün Fiyatı kopyala"}
                                aria-label="Yeni Ürün Fiyatı"
                                onClick={() => copy("yeni", String(yeni))}
                              >
                                {copied === "yeni" ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}
                              </button>
                            </div>
                            <div className="flex items-center justify-between gap-2">
                              <div>
                                Gram Değeri: <b>{gram.toLocaleString("tr-TR")}</b>
                              </div>
                              <button
                                className="inline-flex items-center justify-center align-middle p-1 leading-none"
                                title={copied === "gram" ? "Kopyalandı" : "Gram Değeri kopyala"}
                                aria-label="Gram Değeri"
                                onClick={() => copy("gram", String(gram))}
                              >
                                {copied === "gram" ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}
                              </button>
                            </div>
                            <div className="flex items-center justify-between gap-2">
                              <div>
                                İşçilik (KDV&apos;siz): <b>{isc.toLocaleString("tr-TR", { style: "currency", currency: "TRY" })}</b>
                              </div>
                              <button
                                className="inline-flex items-center justify-center align-middle p-1 leading-none"
                                title={copied === "iscilik" ? "Kopyalandı" : "İşçilik kopyala"}
                                aria-label="İşçilik (KDV&apos;siz)"
                                onClick={() => copy("iscilik", String(isc))}
                              >
                                {copied === "iscilik" ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}
                              </button>
                            </div>
                          </div>
                        );
                      })()}
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
                            const ok = await cancelInvoice(selected)
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




