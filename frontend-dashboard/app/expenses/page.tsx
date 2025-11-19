"use client"
import { t } from '@/lib/i18n'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { api, type Expense, formatDateTimeTr } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Table, TBody, TD, TH, THead, TR } from '@/components/ui/table'
import { Skeleton } from '@/components/ui/skeleton'
import { Button } from '@/components/ui/button'
import { downloadExpensesPdf, downloadExpensesXlsx } from '@/lib/export'
import { IconCopy, IconCheck } from '@/components/ui/icons'

type Filters = {
  start?: string
  end?: string
  q: string
}

const TURKISH_UNITS = ['sıfır', 'bir', 'iki', 'üç', 'dört', 'beş', 'altı', 'yedi', 'sekiz', 'dokuz']
const TURKISH_TENS = ['', 'on', 'yirmi', 'otuz', 'kırk', 'elli', 'altmış', 'yetmiş', 'seksen', 'doksan']
const TURKISH_SCALES = ['', 'bin', 'milyon', 'milyar']

function convertUnderThousand(value: number): string {
  const parts: string[] = []
  const hundreds = Math.floor(value / 100)
  const tens = Math.floor((value % 100) / 10)
  const ones = value % 10
  if (hundreds) {
    parts.push(hundreds === 1 ? 'yüz' : `${TURKISH_UNITS[hundreds]} yüz`)
  }
  if (tens) {
    parts.push(TURKISH_TENS[tens])
  }
  if (ones) {
    parts.push(TURKISH_UNITS[ones])
  }
  return parts.join(' ')
}

function convertNumberToTurkishWords(value: number): string {
  if (!Number.isFinite(value)) return ''
  if (value === 0) return 'sıfır'
  const parts: string[] = []
  let remaining = value
  let scaleIndex = 0
  while (remaining > 0) {
    const chunk = remaining % 1000
    if (chunk) {
      const prefix = convertUnderThousand(chunk)
      const scale = TURKISH_SCALES[scaleIndex]
      let text = prefix
      if (scale) {
        if (scale === 'bin' && chunk === 1) {
          text = 'bin'
        } else {
          text = [prefix, scale].filter(Boolean).join(' ')
        }
      }
      parts.unshift(text)
    }
    remaining = Math.floor(remaining / 1000)
    scaleIndex += 1
  }
  return parts.join(' ')
}

function toTurkishWords(value: number): string {
  if (!Number.isFinite(value)) return ''
  if (value < 0) return `eksi ${toTurkishWords(-value)}`
  const integerPart = Math.floor(value)
  const fraction = Math.round((value - integerPart) * 100)
  const integerText = convertNumberToTurkishWords(integerPart) || 'sıfır'
  const fractionText = fraction ? ` ${convertNumberToTurkishWords(fraction)} kuruş` : ''
  return `${integerText} TL${fractionText}`
}

function normalizeAyar(value?: Expense['altinAyar']): 22 | 24 | null {
  if (value === 22 || value === 'Ayar22') return 22
  if (value === 24 || value === 'Ayar24') return 24
  return null
}

function formatDayMonthYear(input?: string | null): string {
  if (!input) return ''
  const dateOnly = /^\d{4}-\d{2}-\d{2}$/.test(input)
  const parsed = dateOnly ? new Date(`${input}T00:00:00`) : new Date(input)
  if (Number.isNaN(parsed.getTime())) return input
  const dd = String(parsed.getDate()).padStart(2, '0')
  const mm = String(parsed.getMonth() + 1).padStart(2, '0')
  const yyyy = parsed.getFullYear()
  return `${dd}/${mm}/${yyyy}`
}

type ExpensePreviewValues = {
  no: string
  tarih: string
  tutar: string
  tutar1: string
  tutar2: string
  isim: string
  adres: string
  vergi: string
}

function ExpensePreviewCard({ values }: { values: ExpensePreviewValues }) {
  const objectRef = useRef<HTMLObjectElement | null>(null)
  const applyValues = useCallback(() => {
    const doc = objectRef.current?.contentDocument
    if (!doc) return
    const map: [string, string][] = [
      ['field-tarih', values.tarih],
      ['field-no', values.no],
      ['field-tutar', values.tutar],
      ['field-tutar1', values.tutar1],
      ['field-tutar2', values.tutar2],
      ['field-isim', values.isim],
      ['field-adres', values.adres],
      ['field-vergi', values.vergi]
    ]
    map.forEach(([id, text]) => {
      const el = doc.getElementById(id)
      if (el) el.textContent = text
    })
  }, [values])

  useEffect(() => {
    const obj = objectRef.current
    if (!obj) return
    const handleLoad = () => {
      applyValues()
    }
    obj.addEventListener('load', handleLoad)
    applyValues()
    return () => {
      obj.removeEventListener('load', handleLoad)
    }
  }, [applyValues])

  return (
    <>
      <div className="gp-preview">
        <object
          ref={objectRef}
          data="/eren-template.svg"
          type="image/svg+xml"
          aria-label="Gider pusulası önizlemesi"
        />
      </div>
      <style jsx>{`
        .gp-preview {
          background: #f4f4f4;
          padding: 20px;
          display: flex;
          justify-content: center;
          align-items: flex-start;
        }
        .gp-preview object {
          width: 148mm;
          height: 210mm;
          border: none;
          background: #fff;
          box-shadow: 0 4px 12px rgba(0, 0, 0, 0.18);
        }
      `}</style>
    </>
  )
}

function buildPrintHtml(values: ExpensePreviewValues): string {
  const docData = JSON.stringify(values)
  return `
<!doctype html>
<html lang="tr">
<head>
  <meta charset="utf-8">
  <title>Gider Pusulası</title>
  <style>
    body {
      margin: 0;
      background: #ccc;
      display: flex;
      justify-content: center;
      align-items: flex-start;
      padding: 20px;
    }
    object {
      width: 148mm;
      height: 210mm;
      border: none;
      background: #fff;
    }
  </style>
</head>
<body>
  <object id="gpTpl" data="/eren-template.svg" type="image/svg+xml"></object>
  <script>
    const docData = ${docData};
    const fieldMap = [
      ['field-tarih', 'tarih'],
      ['field-no', 'no'],
      ['field-tutar', 'tutar'],
      ['field-tutar1', 'tutar1'],
      ['field-tutar2', 'tutar2'],
      ['field-isim', 'isim'],
      ['field-adres', 'adres'],
      ['field-vergi', 'vergi']
    ];
    const obj = document.getElementById('gpTpl');
    function fill() {
      const svg = obj?.contentDocument;
      if (!svg) return;
      fieldMap.forEach(([id, key]) => {
        const el = svg.getElementById(id);
        if (el) el.textContent = docData[key] ?? '';
      });
    }
    obj?.addEventListener('load', () => {
      fill();
      setTimeout(() => {
        window.print();
        window.onafterprint = () => window.close();
      }, 200);
    });
    fill();
  </script>
</body>
</html>
  `.trim();
}

function triggerPrint(values: ExpensePreviewValues) {
  if (typeof window === 'undefined') return
  const html = buildPrintHtml(values)
  const printWindow = window.open('', '_blank')
  if (!printWindow) return
  printWindow.document.open()
  printWindow.document.write(html)
  printWindow.document.close()
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
  const [urunFiyati, setUrunFiyati] = useState<string>('') 
  const [copied, setCopied] = useState<string | null>(null)
  const [pricingAlert, setPricingAlert] = useState<string | null>(null)
  const [previewOpen, setPreviewOpen] = useState(false)
  const [customAmountWords, setCustomAmountWords] = useState('')
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

  useEffect(() => {
    if (!selected) {
      setCustomAmountWords('')
      setPreviewOpen(false)
      return
    }
    setCustomAmountWords(toTurkishWords(Number(selected.tutar || 0)))
  }, [selected])

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

  const expenseDetails = useMemo(() => {
    if (!selected) return null
    const ayarNumber = normalizeAyar(selected.altinAyar)
    const has = Number(selected.altinSatisFiyati || 0)
    const ay22 = ayarNumber === 22
    const saf = r2(has * (ay22 ? 0.916 : 0.995))
    const yeni = r2(Number(selected.tutar || 0) * (ay22 ? 0.99 : 0.998))
    const gram = saf ? r2(yeni / saf) : 0
    const altinHizmet = r2(gram * saf)
    const iscilikKdvli = r2(r2(Number(selected.tutar || 0)) - altinHizmet)
    const isc = r2(iscilikKdvli / 1.2)
    return { saf, yeni, gram, isc, ayarNumber }
  }, [selected])

  const total = useMemo(() => filtered.reduce((a, b) => a + Number(b.tutar), 0), [filtered])
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize))
  const previewValues = useMemo(() => {
    if (!selected) return null
    const formattedDate = formatDayMonthYear(selected.finalizedAt ?? selected.tarih)
    const ayarNum = expenseDetails?.ayarNumber ?? null
    const gramValue = expenseDetails?.gram ?? 0
    const goldDetail = (gramValue > 0 && ayarNum)
      ? `${gramValue.toLocaleString('tr-TR', { maximumFractionDigits: 2 })} gram ${ayarNum} ALTIN`
      : '-----'
    const amountWords = customAmountWords.trim() || '-----'
    return {
      no: String(selected.siraNo ?? 0).padStart(4, '0'),
      tarih: formattedDate,
      tutar: `# ${Number(selected.tutar || 0).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })} #`,
      tutar1: amountWords,
      tutar2: goldDetail,
      isim: selected.musteriAdSoyad || '-',
      adres: selected.kasiyerAdSoyad || 'SİLİVRİ',
      vergi: selected.tckn || '-'
    }
  }, [selected, expenseDetails, customAmountWords])

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
    setUrunFiyati(String(exp.tutar))
    setModalOpen(true)
  }
  function closeModal() { setModalOpen(false); setSelected(null); setUrunFiyati('') }

  async function handlePrimaryAction() {
    if (!selected) return
    if (selected.kesildi) {
      await toggleStatus(selected)
      closeModal()
      return
    }
    setPreviewOpen(true)
  }

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

  async function handleConfirmPreviewSend() {
    if (!selected) return
    await toggleStatus(selected)
    if (previewValues) {
      triggerPrint(previewValues)
    }
    setPreviewOpen(false)
    closeModal()
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
                      <TD>{x.musteriAdSoyad || '-'}</TD>
                      <TD>{x.tckn || '-'}</TD>
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
                      <div className="flex items-center justify-between gap-2">
                        <div>İsim Soyisim: <b>{selected.musteriAdSoyad || '-'}</b></div>
                        {selected.musteriAdSoyad ? (
                          <button className="inline-flex items-center justify-center align-middle p-1 leading-none" title={copied === 'musteri' ? 'Kopyalandı' : 'Kopyala'} aria-label="Kopyala" onClick={() => copy('musteri', String(selected.musteriAdSoyad))}>{copied === 'musteri' ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}</button>
                        ) : null}
                      </div>
                      <div className="flex items-center justify-between gap-2">
                        <div>T.C. Kimlik No: <b>{selected.tckn || '-'}</b></div>
                        {selected.tckn ? (
                          <button className="inline-flex items-center justify-center align-middle p-1 leading-none" title={copied === 'tckn' ? 'Kopyalandı' : 'Kopyala'} aria-label="Kopyala" onClick={() => copy('tckn', String(selected.tckn))}>{copied === 'tckn' ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}</button>
                        ) : null}
                      </div>
                      <div className="flex items-center justify-between gap-2">
                        <div>Has Altın Fiyatı: <b>{selected.altinSatisFiyati != null ? Number(selected.altinSatisFiyati).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' }) : '-'}</b></div>
                        {selected.altinSatisFiyati ? (
                          <button className="inline-flex items-center justify-center align-middle p-1 leading-none" title={copied === 'altinSatisFiyati' ? 'Kopyalandı' : 'Kopyala'} aria-label="Kopyala" onClick={() => copy('altinSatisFiyati', String(selected.altinSatisFiyati))}>
                            {copied === 'altinSatisFiyati' ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}
                          </button>
                        ) : null}
                      </div>
                      <div>Ayar: <b>{(selected as any).altinAyar ? (((selected as any).altinAyar === 22 || (selected as any).altinAyar === 'Ayar22') ? '22 Ayar' : '24 Ayar') : '-'}</b></div>
                      <div className="flex items-center justify-between gap-2">
                        <div>Ürün Fiyatı: <b>{selected.tutar.toLocaleString("tr-TR", { style: "currency", currency: "TRY" })}</b></div>
                        {selected.tutar ? (
                          <button className="inline-flex items-center justify-center align-middle p-1 leading-none" title={copied === 'tutar' ? 'Kopyalandı' : 'Kopyala'} aria-label="Kopyala" onClick={() => copy('tutar', String(selected.tutar || 0))}>
                            {copied === 'tutar' ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}
                          </button>
                        ) : null}
                      </div>
                      {expenseDetails ? (
                        <div className="mt-2 space-y-1">
                          <div className="flex items-center justify-between gap-2">
                            <div>Saf Altın Değeri: <b>{expenseDetails.saf.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}</b></div>
                            {expenseDetails.saf ? (
                              <button className="inline-flex items-center justify-center align-middle p-1 leading-none" title={copied === 'saf' ? 'Kopyalandı' : 'Kopyala'} aria-label="Kopyala" onClick={() => copy('saf', String(expenseDetails.saf))}>
                                {copied === 'saf' ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}
                              </button>
                            ) : null}
                          </div>
                          <div className="flex items-center justify-between gap-2">
                            <div>Yeni Ürün Fiyatı: <b>{expenseDetails.yeni.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}</b></div>
                            {expenseDetails.yeni ? (
                              <button className="inline-flex items-center justify-center align-middle p-1 leading-none" title={copied === 'yeni' ? 'Kopyalandı' : 'Kopyala'} aria-label="Kopyala" onClick={() => copy('yeni', String(expenseDetails.yeni))}>
                                {copied === 'yeni' ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}
                              </button>
                            ) : null}
                          </div>
                          <div className="flex items-center justify-between gap-2">
                            <div>Gram Değeri: <b>{expenseDetails.gram.toLocaleString('tr-TR', { maximumFractionDigits: 2 })}</b></div>
                            {expenseDetails.gram ? (
                              <button className="inline-flex items-center justify-center align-middle p-1 leading-none" title={copied === 'gram' ? 'Kopyalandı' : 'Kopyala'} aria-label="Kopyala" onClick={() => copy('gram', String(expenseDetails.gram))}>
                                {copied === 'gram' ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}
                              </button>
                            ) : null}
                          </div>
                          <div className="flex items-center justify-between gap-2">
                            <div>İşçilik (KDV&apos;siz): <b>{expenseDetails.isc.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })}</b></div>
                            {expenseDetails.isc ? (
                              <button className="inline-flex items-center justify-center align-middle p-1 leading-none" title={copied === 'isc' ? 'Kopyalandı' : 'Kopyala'} aria-label="Kopyala" onClick={() => copy('isc', String(expenseDetails.isc))}>
                                {copied === 'isc' ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}
                              </button>
                            ) : null}
                          </div>
                        </div>
                      ) : null}
                    </div>
                    <div className="flex flex-wrap items-center justify-end gap-2 pt-2">
                      <Button variant="outline" onClick={closeModal}>{t("modal.close")}</Button>
                      {canToggle && selected && (
                        <Button onClick={handlePrimaryAction}>
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

              {previewOpen && selected && previewValues && (
                <div className="fixed inset-0 bg-black/60 z-[60] flex items-center justify-center px-4">
                  <div className="w-full max-w-3xl max-h-[90vh] overflow-y-auto rounded-2xl bg-white/95 p-5 shadow-2xl dark:bg-slate-900 dark:text-white">
                    <div className="flex items-center justify-between">
                      <h3 className="text-lg font-semibold">Gönderi Önizlemesi</h3>
                      <Button variant="ghost" size="sm" onClick={() => setPreviewOpen(false)}>Kapat</Button>
                    </div>
                    <Label className="mt-3 text-sm text-muted-foreground">Tutar yazı ile</Label>
                    <textarea
                      className="mt-1 h-16 w-full rounded border border-border px-3 py-2 text-sm text-slate-900 focus:border-primary focus:outline-none dark:border-border dark:bg-slate-900 dark:text-white"
                      rows={2}
                      value={customAmountWords}
                      onChange={(e) => setCustomAmountWords(e.target.value)}
                    />
                    <div className="mt-4">
                      <ExpensePreviewCard values={previewValues} />
                    </div>
                    <div className="mt-4 flex flex-wrap items-center justify-end gap-2">
                      <Button variant="outline" onClick={() => setPreviewOpen(false)}>Geri</Button>
                      <Button onClick={handleConfirmPreviewSend}>Gönder ve Kaydet</Button>
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
