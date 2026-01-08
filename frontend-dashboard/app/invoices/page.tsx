"use client"
import { t } from '@/lib/i18n'
import { useEffect, useMemo, useRef, useState } from 'react'
import { api, type Invoice, formatDateTimeTr } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Select } from '@/components/ui/select'
import { Table, TBody, TD, TH, THead, TR } from '@/components/ui/table'
import { Skeleton } from '@/components/ui/skeleton'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { downloadInvoicesPdf, downloadInvoicesXlsx } from '@/lib/export'
import { IconCopy, IconCheck } from '@/components/ui/icons'
import { toast } from 'sonner'
import QRCode from 'qrcode'
import Swal from 'sweetalert2'
import 'sweetalert2/dist/sweetalert2.min.css'

type CompanyInfo = {
  companyName?: string | null
  taxNo?: string | null
  address?: string | null
  tradeRegistryNo?: string | null
  phone?: string | null
  email?: string | null
  cityName?: string | null
  townName?: string | null
  postalCode?: string | null
  taxOfficeName?: string | null
}

type PreviewLine = {
  name: string
  quantity: string
  unit: string
  unitPrice: string
  discountRate: string
  discountAmount: string
  vatRate: string
  vatAmount: string
  otherTaxes: string
  lineTotal: string
}

type PreviewInvoice = {
  isArchive: boolean
  companyTaxCode: string
  companyAddress: string
  companyCity: string
  companyPostalCode: string
  companyTaxOffice: string
  companyEmail: string
  externalCode: string
  invoiceDate: string
  invoiceType: string
  orderNumber: string
  receiverName: string
  receiverTaxCode: string
  receiverCity: string
  receiverPostalCode: string
  receiverTaxOffice: string
  receiverEmail: string
  sendingType: string
  scenarioType: string
  totalLineExtension: string
  totalDiscount: string
  totalPayable: string
  totalTaxInclusive: string
  totalVat: string
  details: PreviewLine[]
}

const getNodeText = (root: ParentNode, localName: string) => {
  const el = (root as Document).getElementsByTagNameNS
    ? (root as Document).getElementsByTagNameNS('*', localName)[0]
    : null
  return el?.textContent?.trim() || ''
}

const getChildText = (root: ParentNode | null, localName: string) => {
  if (!root) return ''
  const el = (root as Element).getElementsByTagNameNS('*', localName)[0]
  return el?.textContent?.trim() || ''
}

function parseTurmobXml(xml: string): PreviewInvoice | null {
  try {
    const doc = new DOMParser().parseFromString(xml, 'text/xml')
    const archive = doc.getElementsByTagNameNS('*', 'ArchiveInvoice')[0]
    const invoice = archive || doc.getElementsByTagNameNS('*', 'Invoice')[0]
    if (!invoice) return null
    const isArchive = Boolean(archive)

    const companyAddressNode = invoice.getElementsByTagNameNS('*', 'CompanyBranchAddress')[0]
    const receiverNode = invoice.getElementsByTagNameNS('*', 'Receiver')[0]
    const receiverBranchNode = invoice.getElementsByTagNameNS('*', 'ReceiverBranchAddress')[0]

    const detailsRootName = isArchive ? 'ArchiveInvoiceDetail' : 'InvoiceDetail'
    const detailNodes = Array.from(invoice.getElementsByTagNameNS('*', detailsRootName))

    const details = detailNodes.map((detail) => {
      const productNode = detail.getElementsByTagNameNS('*', 'Product')[0]
      return {
        name: getChildText(productNode, 'ProductName'),
        quantity: getChildText(detail, 'Quantity'),
        unit: getChildText(productNode, 'MeasureUnit'),
        unitPrice: getChildText(productNode, 'UnitPrice'),
        discountRate: '0',
        discountAmount: '0',
        vatRate: getChildText(detail, 'VATRate'),
        vatAmount: getChildText(detail, 'VATAmount'),
        otherTaxes: '0',
        lineTotal: getChildText(detail, 'LineExtensionAmount')
      }
    })

    return {
      isArchive,
      companyTaxCode: getNodeText(doc, 'CompanyTaxCode'),
      companyAddress: getChildText(companyAddressNode, 'BoulevardAveneuStreetName'),
      companyCity: getChildText(companyAddressNode, 'CityName'),
      companyPostalCode: getChildText(companyAddressNode, 'PostalCode'),
      companyTaxOffice: getChildText(companyAddressNode, 'TaxOfficeName'),
      companyEmail: getChildText(companyAddressNode, 'EMail'),
      externalCode: getChildText(invoice, isArchive ? 'ExternalArchiveInvoiceCode' : 'ExternalInvoiceCode'),
      invoiceDate: getChildText(invoice, 'InvoiceDate'),
      invoiceType: getChildText(invoice, 'InvoiceType'),
      orderNumber: getChildText(invoice, 'OrderNumber'),
      receiverName: getChildText(receiverNode, 'ReceiverName'),
      receiverTaxCode: getChildText(receiverNode, 'ReceiverTaxCode'),
      receiverCity: getChildText(receiverBranchNode, 'CityName'),
      receiverPostalCode: getChildText(receiverBranchNode, 'PostalCode'),
      receiverTaxOffice: getChildText(receiverBranchNode, 'TaxOfficeName'),
      receiverEmail: getChildText(receiverBranchNode, 'EMail'),
      sendingType: getChildText(receiverNode, 'SendingType'),
      scenarioType: getChildText(invoice, 'ScenarioType') || 'TR1.2',
      totalLineExtension: getChildText(invoice, 'TotalLineExtensionAmount'),
      totalDiscount: getChildText(invoice, 'TotalDiscountAmount'),
      totalPayable: getChildText(invoice, 'TotalPayableAmount'),
      totalTaxInclusive: getChildText(invoice, 'TotalTaxInclusiveAmount'),
      totalVat: getChildText(invoice, 'TotalVATAmount'),
      details
    }
  } catch {
    return null
  }
}

function formatXml(xml: string): string {
  try {
    const doc = new DOMParser().parseFromString(xml, 'text/xml')
    if (doc.getElementsByTagName('parsererror').length > 0) return xml
    const serialized = new XMLSerializer().serializeToString(doc)
    const withLines = serialized.replace(/(>)(<)(\/*)/g, '$1\n$2$3')
    let indent = 0
    return withLines.split('\n').map((line) => {
      if (line.match(/^<\/\w/)) indent = Math.max(indent - 2, 0)
      const padding = ' '.repeat(indent)
      if (line.match(/^<\w[^>]*[^/]>.*$/)) indent += 2
      return `${padding}${line}`
    }).join('\n')
  } catch {
    return xml
  }
}

const formatMoney = (value: string | number, suffix = 'TL') => {
  const num = typeof value === 'number' ? value : Number(value || 0)
  if (!Number.isFinite(num)) return `0,00 ${suffix}`
  const formatted = num.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
  return `${formatted} ${suffix}`
}

const formatQty = (value: string | number) => {
  const num = typeof value === 'number' ? value : Number(value || 0)
  if (!Number.isFinite(num)) return '0,00'
  return num.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 3 })
}

const normalizeAyar = (value: Invoice['altinAyar'] | undefined): 22 | 24 => {
  return value === 22 || value === 'Ayar22' ? 22 : 24
}

const toNumber = (value: string | number) => {
  if (typeof value === 'number') return value
  if (!value) return 0
  const normalized = value.replace(',', '.')
  const num = Number(normalized)
  return Number.isFinite(num) ? num : 0
}

const formatInputNumber = (value: number, decimals: number) => {
  if (!Number.isFinite(value)) return ''
  return value.toFixed(decimals)
}

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
  const [summaryOpen, setSummaryOpen] = useState(false)
  const [summaryCustomer, setSummaryCustomer] = useState<Invoice | null>(null)
  const [showTcknFromVkn, setShowTcknFromVkn] = useState(false)
  const [copied, setCopied] = useState<string | null>(null)
  const [pricingAlert, setPricingAlert] = useState<string | null>(null)
  const [xmlOpen, setXmlOpen] = useState(false)
  const [xmlLoading, setXmlLoading] = useState(false)
  const [xmlAction, setXmlAction] = useState<string | null>(null)
  const [xmlPreview, setXmlPreview] = useState<string | null>(null)
  const [xmlError, setXmlError] = useState<string | null>(null)
  const [xmlSendLoading, setXmlSendLoading] = useState(false)
  const [xmlSendResult, setXmlSendResult] = useState<string | null>(null)
  const [xmlSendResultKind, setXmlSendResultKind] = useState<'error' | 'info' | null>(null)
  const [turmobDisabled, setTurmobDisabled] = useState(false)
  const [companyInfo, setCompanyInfo] = useState<CompanyInfo | null>(null)
  const [previewData, setPreviewData] = useState<PreviewInvoice | null>(null)
  const [xmlView, setXmlView] = useState<'preview' | 'xml'>('preview')
  const [qrDataUrl, setQrDataUrl] = useState<string | null>(null)
  const [pdfUrl, setPdfUrl] = useState<string | null>(null)
  const [editTutar, setEditTutar] = useState('')
  const [editGram, setEditGram] = useState('')
  const [editMode, setEditMode] = useState<'tutar' | 'gram'>('tutar')
  const [editAyar, setEditAyar] = useState<22 | 24>(22)
  const [editingField, setEditingField] = useState<'tutar' | 'gram' | 'ayar' | null>(null)
  const [savingField, setSavingField] = useState<'tutar' | 'gram' | null>(null)
  const [editError, setEditError] = useState<string | null>(null)
  const previewWrapperRef = useRef<HTMLDivElement | null>(null)
  const previewPageRef = useRef<HTMLDivElement | null>(null)
  async function copy(key: string, text: string) {
    try {
      const s = (text ?? '').toString().trim()
      const forClipboard = /^-?\d+\.\d+$/.test(s) ? s.replace(/\./g, ',') : s
      await navigator.clipboard.writeText(forClipboard)
      setCopied(key)
      setTimeout(() => setCopied(null), 1500)
    } catch {}
  }

  function authHeaders(): HeadersInit {
    try {
      const token = localStorage.getItem('ktp_token') || (typeof document !== 'undefined' ? (document.cookie.split('; ').find(x => x.startsWith('ktp_token='))?.split('=')[1] || '') : '')
      return token ? { Authorization: `Bearer ${token}` } : {}
    } catch { return {} }
  }

  function openCustomerSummary(inv: Invoice) {
    setSummaryCustomer(inv)
    setSummaryOpen(true)
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

  useEffect(() => {
    let mounted = true
    ;(async () => {
      try {
        const base = process.env.NEXT_PUBLIC_API_BASE || ''
        const res = await fetch(`${base}/api/company-info`, { cache: 'no-store', headers: { ...authHeaders() } })
        if (!mounted) return
        if (res.ok) {
          setCompanyInfo(await res.json())
        } else {
          setCompanyInfo(null)
        }
      } catch {
        if (!mounted) return
        setCompanyInfo(null)
      }
    })()
    return () => { mounted = false }
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
        if (res.ok) { const j = await res.json(); setCanToggle(Boolean(j?.canToggleKesildi) || String(j?.role) === 'Yonetici') }
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

  useEffect(() => {
    if (!modalOpen) {
      setShowTcknFromVkn(false)
      return
    }
    setShowTcknFromVkn(false)
  }, [modalOpen, selected?.id])

  useEffect(() => {
    if (!modalOpen || !selected) return
    const initialTutar = Number(selected.tutar || 0)
    const ayar = normalizeAyar(selected.altinAyar)
    const safOran = ayar === 22 ? 0.916 : 0.995
    const yeniOran = ayar === 22 ? 0.99 : 0.998
    const saf = r2(Number(selected.altinSatisFiyati || 0) * safOran)
    const yeni = r2(initialTutar * yeniOran)
    const gram = selected.gramDegeri != null
      ? Number(selected.gramDegeri)
      : (saf > 0 ? r2(yeni / saf) : 0)
    setEditTutar(formatInputNumber(initialTutar, 2))
    setEditGram(formatInputNumber(gram, 3))
    setEditMode(selected.gramDegeri != null ? 'gram' : 'tutar')
    setEditAyar(ayar)
    setEditingField(null)
    setEditError(null)
  }, [modalOpen, selected?.id])

  useEffect(() => {
    if (!xmlPreview) {
      setPreviewData(null)
      return
    }
    setPreviewData(parseTurmobXml(xmlPreview))
  }, [xmlPreview])

  useEffect(() => {
    if (!selected || editMode !== 'tutar') return
    if (editingField !== 'tutar' && selected.gramDegeri != null) return
    const safOran = editAyar === 22 ? 0.916 : 0.995
    const yeniOran = editAyar === 22 ? 0.99 : 0.998
    const saf = r2(Number(selected.altinSatisFiyati || 0) * safOran)
    const yeni = r2(toNumber(editTutar) * yeniOran)
    const gram = saf > 0 ? r2(yeni / saf) : 0
    const next = formatInputNumber(gram, 3)
    if (next !== editGram) setEditGram(next)
  }, [editTutar, editMode, editGram, selected, r2, editAyar, editingField])

  const formattedXml = useMemo(() => {
    return xmlPreview ? formatXml(xmlPreview) : ''
  }, [xmlPreview])

  const invoiceCalc = useMemo(() => {
    if (!selected) return null
    const safOran = editAyar === 22 ? 0.916 : 0.995
    const yeniOran = editAyar === 22 ? 0.99 : 0.998
    const saf = r2(Number(selected.altinSatisFiyati || 0) * safOran)
    const tutar = toNumber(editTutar)
    const gramInput = toNumber(editGram)
    const gram = editMode === 'gram'
      ? gramInput
      : (saf > 0 ? r2(r2(tutar * yeniOran) / saf) : 0)
    const altinHizmet = r2(saf * gram)
    const yeni = editMode === 'gram' ? altinHizmet : r2(tutar * yeniOran)
    const iscilikKdvli = r2(tutar - altinHizmet)
    const iscilik = r2(iscilikKdvli / 1.20)
    const kdvTutar = r2(iscilikKdvli - iscilik)
    return { saf, yeni, gram, altinHizmet, iscilik, kdvTutar, tutar }
  }, [selected, editTutar, editGram, editMode, r2, editAyar])

  async function saveInvoiceEdits(mode: 'tutar' | 'gram') {
    if (!selected) return
    setSavingField(mode)
    setEditError(null)
    try {
      const resp = await api.updateInvoicePreview(selected.id, {
        tutar: toNumber(editTutar),
        gramDegeri: toNumber(editGram),
        mode,
        altinAyar: editAyar
      })
      setSelected((prev) => prev ? {
        ...prev,
        tutar: resp.tutar,
        safAltinDegeri: resp.safAltinDegeri ?? null,
        urunFiyati: resp.urunFiyati ?? null,
        yeniUrunFiyati: resp.yeniUrunFiyati ?? null,
        gramDegeri: resp.gramDegeri ?? null,
        iscilik: resp.iscilik ?? null,
        altinAyar: editAyar
      } : prev)
      setData((prev) => prev ? prev.map((x) => x.id === selected.id ? {
        ...x,
        tutar: resp.tutar,
        safAltinDegeri: resp.safAltinDegeri ?? null,
        urunFiyati: resp.urunFiyati ?? null,
        yeniUrunFiyati: resp.yeniUrunFiyati ?? null,
        gramDegeri: resp.gramDegeri ?? null,
        iscilik: resp.iscilik ?? null,
        altinAyar: editAyar
      } : x) : prev)
      setEditTutar(formatInputNumber(resp.tutar, 2))
      setEditGram(formatInputNumber(resp.gramDegeri ?? 0, 3))
      setEditingField(null)
      setEditMode(mode)
    } catch {
      setEditError('Güncelleme başarısız.')
    } finally {
      setSavingField(null)
    }
  }

  function cancelInvoiceEdits() {
    if (!selected) return
    const initialTutar = Number(selected.tutar || 0)
    const ayar = normalizeAyar(selected.altinAyar)
    const safOran = ayar === 22 ? 0.916 : 0.995
    const yeniOran = ayar === 22 ? 0.99 : 0.998
    const saf = r2(Number(selected.altinSatisFiyati || 0) * safOran)
    const yeni = r2(initialTutar * yeniOran)
    const gram = selected.gramDegeri != null
      ? Number(selected.gramDegeri)
      : (saf > 0 ? r2(yeni / saf) : 0)
    setEditTutar(formatInputNumber(initialTutar, 2))
    setEditGram(formatInputNumber(gram, 3))
    setEditMode(selected.gramDegeri != null ? 'gram' : 'tutar')
    setEditAyar(ayar)
    setEditingField(null)
    setEditError(null)
  }

  useEffect(() => {
    let mounted = true
    if (!xmlPreview || !selected) {
      setQrDataUrl(null)
      setPdfUrl(null)
      return () => { mounted = false }
    }
    const origin = typeof window !== 'undefined' ? window.location.origin : ''
    const url = `${origin}/invoice-sample.pdf?invoiceId=${selected.id}`
    setPdfUrl(url)
    QRCode.toDataURL(url, { width: 180, margin: 0 })
      .then((dataUrl) => { if (mounted) setQrDataUrl(dataUrl) })
      .catch(() => { if (mounted) setQrDataUrl(null) })
    return () => { mounted = false }
  }, [xmlPreview, selected?.id])


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

  async function openXmlPreview(inv: Invoice) {
    setXmlError(null)
    setXmlSendResult(null)
    setXmlSendResultKind(null)
    setXmlAction(null)
    setXmlPreview(null)
    setXmlView('preview')
    setXmlOpen(true)
    setXmlLoading(true)
    try {
      const resp = await api.previewTurmobInvoice(inv.id)
      setXmlAction(resp.action)
      setXmlPreview(resp.xml)
    } catch {
      setXmlError('XML oluşturulamadı')
    } finally {
      setXmlLoading(false)
    }
  }

  async function markInvoiceSent(inv: Invoice) {
    try {
      await api.setInvoiceStatus(inv.id, true)
      setData((prev) => prev ? prev.map(x => x.id === inv.id ? { ...x, kesildi: true } : x) : prev)
      setSelected((prev) => prev && prev.id === inv.id ? { ...prev, kesildi: true } : prev)
      try { window.dispatchEvent(new CustomEvent('ktp:tx-updated')) } catch {}
      return true
    } catch {
      return false
    }
  }

  async function sendTurmob(inv: Invoice) {
    setXmlSendLoading(true)
    setXmlSendResult(null)
    setXmlSendResultKind(null)
    try {
      const resp = await api.sendTurmobInvoice(inv.id)
      if (resp.status === 'Success') {
        setTurmobDisabled(false)
        toast.success('Fatura başarıyla gönderildi')
        const ok = await markInvoiceSent(inv)
        if (ok) {
          setXmlOpen(false)
          setModalOpen(false)
          setSelected(null)
        } else {
          setXmlSendResult('Gönderildi ama durum güncellenemedi')
          setXmlSendResultKind('info')
        }
      } else if (resp.status === 'Skipped') {
        setTurmobDisabled(true)
        const ok = await markInvoiceSent(inv)
        if (ok) {
          toast.success('TURMOB kapalı, fatura kesildi')
          setXmlOpen(false)
          setModalOpen(false)
          setSelected(null)
        } else {
          setXmlSendResult('TURMOB kapalı ama durum güncellenemedi')
          setXmlSendResultKind('info')
        }
      } else {
        setXmlSendResult(resp.errorMessage ? `Gönderilemedi: ${resp.errorMessage}` : 'Gönderilemedi')
        setXmlSendResultKind('error')
      }
    } catch {
      setXmlSendResult('Gönderme başarısız')
      setXmlSendResultKind('error')
    } finally {
      setXmlSendLoading(false)
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

  async function deleteInvoice(inv: Invoice) {
    if (!inv || inv.kesildi) return false
    const result = await Swal.fire({
      title: 'Fatura silinsin mi?',
      text: 'Bu işlem geri alınamaz.',
      icon: 'warning',
      showCancelButton: true,
      confirmButtonText: 'Sil',
      cancelButtonText: 'Vazgeç',
      reverseButtons: true,
      showClass: { popup: 'swal2-show' },
      hideClass: { popup: 'swal2-hide' }
    })
    if (!result.isConfirmed) return false
    try {
      await api.deleteInvoice(inv.id)
      setData(prev => prev ? prev.filter(x => x.id !== inv.id) : prev)
      try {
        const base = process.env.NEXT_PUBLIC_API_BASE || ''
        const bust = `&ts=${Date.now()}`
        await fetch(`${base}/api/invoices?page=1&pageSize=500${bust}`, { cache: 'no-store', headers: { ...authHeaders() } })
      } catch {}
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
                    <TH>TCKN / VKN</TH>
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
                      <TD>{x.isForCompany ? (x.companyName || x.musteriAdSoyad || '-') : (x.musteriAdSoyad || '-')}</TD>
                      <TD>{x.isForCompany ? (x.vknNo || '-') : (x.tckn || '-')}</TD>
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
                    <div className="grid grid-cols-[140px_1fr_auto] items-center gap-x-2 gap-y-2 text-sm">
                      {!selected.isForCompany && (
                        <div className="contents">
                          <div>İsim Soyisim:</div>
                          <div className="font-semibold">{selected.musteriAdSoyad || '-'}</div>
                          {selected.musteriAdSoyad ? (
                            <button className="inline-flex items-center justify-center align-middle p-1 leading-none" title={copied === 'musteri' ? 'Kopyalandı' : 'Kopyala'} aria-label="Kopyala" onClick={() => copy('musteri', String(selected.musteriAdSoyad))}>
                              {copied === 'musteri' ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}
                            </button>
                          ) : (
                            <span />
                          )}
                        </div>
                      )}
                      {selected.isForCompany && (
                        <div className="contents">
                          <div>Şirket Adı:</div>
                          <div>
                            <button
                              type="button"
                              className="font-semibold underline underline-offset-4"
                              onClick={() => openCustomerSummary(selected)}
                            >
                              {selected.companyName}
                            </button>
                          </div>
                          <span />
                        </div>
                      )}
                      {selected.isForCompany ? (
                        <div className="contents">
                          <div>VKN:</div>
                          <div>
                            <button
                              type="button"
                              className="font-semibold underline underline-offset-4"
                              onClick={() => setShowTcknFromVkn((v) => !v)}
                            >
                              {selected.vknNo || '-'}
                            </button>
                          </div>
                          {selected.vknNo ? (
                            <button className="inline-flex items-center justify-center align-middle p-1 leading-none" title={copied === 'vkn' ? 'Kopyalandı' : 'Kopyala'} aria-label="Kopyala" onClick={() => copy('vkn', String(selected.vknNo))}>
                              {copied === 'vkn' ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}
                            </button>
                          ) : (
                            <span />
                          )}
                        </div>
                      ) : (
                        <div className="contents">
                          <div>T.C. Kimlik No:</div>
                          <div className="font-semibold">{selected.tckn || '-'}</div>
                          {selected.tckn ? (
                            <button className="inline-flex items-center justify-center align-middle p-1 leading-none" title={copied === 'tckn' ? 'Kopyalandı' : 'Kopyala'} aria-label="Kopyala" onClick={() => copy('tckn', String(selected.tckn))}>
                              {copied === 'tckn' ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}
                            </button>
                          ) : (
                            <span />
                          )}
                        </div>
                      )}
                      {selected.isForCompany && showTcknFromVkn && (
                        <div className="contents">
                          <div>T.C. Kimlik No:</div>
                          <div className="font-semibold">{selected.tckn || '-'}</div>
                          {selected.tckn ? (
                            <button className="inline-flex items-center justify-center align-middle p-1 leading-none" title={copied === 'tckn' ? 'Kopyalandı' : 'Kopyala'} aria-label="Kopyala" onClick={() => copy('tckn', String(selected.tckn))}>
                              {copied === 'tckn' ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}
                            </button>
                          ) : (
                            <span />
                          )}
                        </div>
                      )}
                      <div className="contents">
                        <div>Ayar:</div>
                        {editingField === 'ayar' ? (
                          <div className="flex items-center gap-1">
                            <select
                              value={editAyar}
                              onChange={(e) => setEditAyar(Number(e.target.value) as 22 | 24)}
                              className="h-8 rounded border border-slate-300 bg-white px-2 text-sm text-slate-900"
                            >
                              <option value={22}>22 Ayar</option>
                              <option value={24}>24 Ayar</option>
                            </select>
                            <button
                              className="inline-flex items-center justify-center rounded px-1 text-sm"
                              onClick={() => saveInvoiceEdits('tutar')}
                              disabled={savingField === 'tutar'}
                              title="Kaydet"
                            >
                              ✅
                            </button>
                            <button
                              className="inline-flex items-center justify-center rounded px-1 text-sm"
                              onClick={cancelInvoiceEdits}
                              title="Vazgeç"
                            >
                              ❌
                            </button>
                          </div>
                        ) : (
                          <b>{editAyar === 22 ? '22 Ayar' : '24 Ayar'}</b>
                        )}
                        {editingField === 'ayar' ? (
                          <span />
                        ) : (
                          <button
                            className="inline-flex items-center justify-center rounded px-1 text-sm"
                            onClick={() => {
                              setEditingField('ayar')
                              setEditError(null)
                            }}
                            title="Düzenle"
                          >
                            ✏️
                          </button>
                        )}
                      </div>
                      <div className="contents">
                        <div>Gram:</div>
                        <div className="flex items-center gap-2">
                          {editingField === 'gram' ? (
                            <div className="flex items-center gap-1">
                              <Input
                                value={editGram}
                                inputMode="decimal"
                                onChange={(e) => {
                                  setEditMode('gram')
                                  setEditGram(e.target.value)
                                }}
                                className="h-8 w-28 text-right"
                              />
                              <button
                                className="inline-flex items-center justify-center rounded px-1 text-sm"
                                onClick={() => saveInvoiceEdits('gram')}
                                disabled={savingField === 'gram'}
                                title="Kaydet"
                              >
                                ✅
                              </button>
                              <button
                                className="inline-flex items-center justify-center rounded px-1 text-sm"
                                onClick={cancelInvoiceEdits}
                                title="Vazgeç"
                              >
                                ❌
                              </button>
                            </div>
                          ) : (
                            <>
                              <b className="tabular-nums">{invoiceCalc ? invoiceCalc.gram.toLocaleString("tr-TR") : '-'}</b>
                              <button
                                className="inline-flex items-center justify-center rounded px-1 text-sm"
                                onClick={() => {
                                  setEditMode('gram')
                                  setEditingField('gram')
                                  setEditError(null)
                                }}
                                title="Düzenle"
                              >
                                ✏️
                              </button>
                            </>
                          )}
                        </div>
                        {invoiceCalc ? (
                          <button className="inline-flex items-center justify-center align-middle p-1 leading-none" title={copied === 'gram' ? 'Kopyalandı' : 'Gram kopyala'} aria-label="Gram" onClick={() => copy('gram', String(invoiceCalc.gram))}>
                            {copied === 'gram' ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}
                          </button>
                        ) : (
                          <span />
                        )}
                      </div>
                      {editError && (
                        <div className="col-span-3 rounded border border-rose-200 bg-rose-50 px-2 py-1 text-xs text-rose-700">
                          {editError}
                        </div>
                      )}
                      {invoiceCalc && (
                        <div className="col-span-3 mt-2 space-y-1">
                          <div className="grid grid-cols-[140px_1fr_auto] items-center gap-2">
                            <div>Ürün Has Değeri:</div>
                            <div className="font-semibold tabular-nums">{invoiceCalc.yeni.toLocaleString("tr-TR", { style: "currency", currency: "TRY" })}</div>
                            <button
                              className="inline-flex items-center justify-center align-middle p-1 leading-none"
                              title={copied === "yeni" ? "Kopyalandı" : "Ürün Has Değeri kopyala"}
                              aria-label="Ürün Has Değeri"
                              onClick={() => copy("yeni", String(invoiceCalc.yeni))}
                            >
                              {copied === "yeni" ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}
                            </button>
                          </div>
                          <div className="grid grid-cols-[140px_1fr_auto] items-center gap-2">
                            <div>İşçilik (KDV&apos;siz):</div>
                            <div className="font-semibold tabular-nums">{invoiceCalc.iscilik.toLocaleString("tr-TR", { style: "currency", currency: "TRY" })}</div>
                            <button
                              className="inline-flex items-center justify-center align-middle p-1 leading-none"
                              title={copied === "iscilik" ? "Kopyalandı" : "İşçilik kopyala"}
                              aria-label="İşçilik (KDV&apos;siz)"
                              onClick={() => copy("iscilik", String(invoiceCalc.iscilik))}
                            >
                              {copied === "iscilik" ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}
                            </button>
                          </div>
                          <div className="grid grid-cols-[140px_1fr_auto] items-center gap-2">
                            <div>KDV Tutarı:</div>
                            <div className="font-semibold tabular-nums">{invoiceCalc.kdvTutar.toLocaleString("tr-TR", { style: "currency", currency: "TRY" })}</div>
                            <button
                              className="inline-flex items-center justify-center align-middle p-1 leading-none"
                              title={copied === "kdv" ? "Kopyalandı" : "KDV Tutarı kopyala"}
                              aria-label="KDV Tutarı"
                              onClick={() => copy("kdv", String(invoiceCalc.kdvTutar))}
                            >
                              {copied === "kdv" ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}
                            </button>
                          </div>
                          <div className="grid grid-cols-[140px_1fr_auto] items-center gap-2 font-semibold">
                            <div>Ürün Tutarı:</div>
                            <div className="flex items-center gap-2">
                              {editingField === 'tutar' ? (
                                <div className="flex items-center gap-1 font-normal">
                                  <Input
                                    value={editTutar}
                                    inputMode="decimal"
                                    onChange={(e) => {
                                      setEditMode('tutar')
                                      setEditTutar(e.target.value)
                                    }}
                                    className="h-8 w-32 text-right"
                                  />
                                  <button
                                    className="inline-flex items-center justify-center rounded px-1 text-sm"
                                    onClick={() => saveInvoiceEdits('tutar')}
                                    disabled={savingField === 'tutar'}
                                    title="Kaydet"
                                  >
                                    ✅
                                  </button>
                                  <button
                                    className="inline-flex items-center justify-center rounded px-1 text-sm"
                                    onClick={cancelInvoiceEdits}
                                    title="Vazgeç"
                                  >
                                    ❌
                                  </button>
                                </div>
                              ) : (
                                <>
                                  <b className="tabular-nums">{invoiceCalc.tutar.toLocaleString("tr-TR", { style: "currency", currency: "TRY" })}</b>
                                  <button
                                    className="inline-flex items-center justify-center rounded px-1 text-sm"
                                    onClick={() => {
                                      setEditMode('tutar')
                                      setEditingField('tutar')
                                      setEditError(null)
                                    }}
                                    title="Düzenle"
                                  >
                                    ✏️
                                  </button>
                                </>
                              )}
                            </div>
                            <button className="inline-flex items-center justify-center align-middle p-1 leading-none" title={copied === 'tutar' ? 'Kopyalandı' : 'Kopyala'} aria-label="Kopyala" onClick={() => copy('tutar', String(invoiceCalc.tutar))}>
                              {copied === 'tutar' ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}
                            </button>
                          </div>
                          <div className="text-slate-400">----------------------------------</div>
                          <div className="grid grid-cols-[140px_1fr] items-center gap-2">
                            <div>Has Altın Fiyatı:</div>
                            <div className="font-semibold tabular-nums">{selected.altinSatisFiyati != null ? Number(selected.altinSatisFiyati).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' }) : '-'}</div>
                          </div>
                          <div className="grid grid-cols-[140px_1fr_auto] items-center gap-2">
                            <div>Has Altın Değeri:</div>
                            <div className="font-semibold tabular-nums">{invoiceCalc.saf.toLocaleString("tr-TR", { style: "currency", currency: "TRY" })}</div>
                            <button
                              className="inline-flex items-center justify-center align-middle p-1 leading-none"
                              title={copied === "saf" ? "Kopyalandı" : "Has Altın Değeri kopyala"}
                              aria-label="Has Altın Değeri"
                              onClick={() => copy("saf", String(invoiceCalc.saf))}
                            >
                              {copied === "saf" ? <IconCheck width={14} height={14} /> : <IconCopy width={14} height={14} />}
                            </button>
                          </div>
                        </div>
                      )}
                    </div>
                    <div className="flex flex-wrap items-center justify-end gap-2 pt-2">
                      <Button variant="outline" onClick={closeModal}>{t("modal.close")}</Button>
                      {canToggle && selected && (turmobDisabled || !selected.kesildi) && (
                        <Button
                          onClick={async () => {
                            if (turmobDisabled) {
                              await toggleStatus(selected)
                              closeModal()
                              return
                            }
                            if (!selected.kesildi) {
                              await openXmlPreview(selected)
                            }
                          }}
                        >
                          {turmobDisabled ? (selected.kesildi ? 'Geri Al' : 'Gönder') : 'Gönder'}
                        </Button>
                      )}
                      {selected && !selected.kesildi && (
                        <Button
                          variant="default"
                          onClick={async () => {
                            const ok = await deleteInvoice(selected)
                            if (ok) closeModal()
                          }}
                        >
                          Sil
                        </Button>
                      )}
                    </div>
                  </div>
                </div>
              )}
              <Dialog open={xmlOpen} onOpenChange={setXmlOpen}>
                <DialogContent className="fixed inset-0 w-screen h-screen max-w-none max-h-none p-0 overflow-hidden">
                  <div className="flex h-full flex-col">
                    <div className="shrink-0 border-b border-slate-800 bg-slate-950 px-4 py-3 text-white">
                      <DialogHeader>
                        <DialogTitle>XML Önizleme</DialogTitle>
                      </DialogHeader>
                      {xmlAction && (
                        <div className="mt-1 text-sm text-slate-200">İşlem: <b>{xmlAction}</b></div>
                      )}
                      {xmlError && (
                        <div className="mt-2 rounded border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700">
                          {xmlError}
                        </div>
                      )}
                      {xmlLoading && (
                        <div className="mt-2 text-sm text-slate-300">XML hazırlanıyor...</div>
                      )}
                    </div>
                    <div className="flex-1 relative">
                      {xmlView === 'preview' && xmlPreview && previewData && (
                        <div ref={previewWrapperRef} className="absolute inset-0 overflow-auto bg-slate-100">
                          <div
                            ref={previewPageRef}
                            className="mx-auto w-[820px] min-h-[1120px] bg-white border border-slate-700 px-6 py-5 text-[11px] leading-[1.25] text-slate-900"
                          >
                          <div className="grid grid-cols-[1.25fr_0.9fr_0.9fr] gap-6">
                            <div className="border-y border-slate-800 py-2 text-[10px] uppercase">
                              <div className="font-semibold">{companyInfo?.companyName || 'Firma Adı'}</div>
                              <div>{companyInfo?.address || previewData.companyAddress || '-'}</div>
                              <div>{(companyInfo?.postalCode || previewData.companyPostalCode || '-') + ' ' + (companyInfo?.cityName || previewData.companyCity || '')}</div>
                              <div>Telefon: {companyInfo?.phone || '-'}</div>
                              <div>E-Posta: {companyInfo?.email || previewData.companyEmail || '-'}</div>
                              <div>Vergi Dairesi: {companyInfo?.taxOfficeName || previewData.companyTaxOffice || '-'}</div>
                              <div>VKN: {companyInfo?.taxNo || previewData.companyTaxCode || '-'}</div>
                            </div>
                            <div className="flex flex-col items-center gap-2 pt-1 text-center">
                              <div className="flex h-16 w-16 items-center justify-center rounded-full border border-slate-400 text-[10px] font-semibold">GİB</div>
                              <div className="text-base font-semibold">e-Arşiv Fatura</div>
                              <div className="text-[11px] italic text-slate-600">e-imza</div>
                            </div>
                            <div className="flex flex-col items-end gap-4">
                              <div className="flex flex-col items-end gap-2">
                                {qrDataUrl ? (
                                  <img src={qrDataUrl} alt="QR" className="h-[140px] w-[140px] border border-slate-300" />
                                ) : (
                                  <div className="flex h-[140px] w-[140px] items-center justify-center border border-dashed border-slate-300 text-[10px] text-slate-400">QR</div>
                                )}
                                {pdfUrl ? (
                                  <div className="text-[10px] text-slate-500">PDF: {pdfUrl}</div>
                                ) : null}
                              </div>
                              <img src="/erenkuyumculuklogo.png" alt="Eren Kuyumculuk" className="h-[80px] w-auto object-contain" />
                              <div className="grid w-full max-w-[210px] grid-cols-[1fr_1fr] border border-slate-700 text-[10px]">
                                <div className="border-b border-r border-slate-700 px-2 py-1 font-semibold">Özelleştirme No:</div>
                                <div className="border-b border-slate-700 px-2 py-1">{previewData.scenarioType || 'TR1.2'}</div>
                                <div className="border-b border-r border-slate-700 px-2 py-1 font-semibold">Fatura No:</div>
                                <div className="border-b border-slate-700 px-2 py-1">{previewData.externalCode || '-'}</div>
                                <div className="border-b border-r border-slate-700 px-2 py-1 font-semibold">Fatura Tipi:</div>
                                <div className="border-b border-slate-700 px-2 py-1">{previewData.invoiceType || 'SATIS'}</div>
                                <div className="border-b border-r border-slate-700 px-2 py-1 font-semibold">Gönderim Şekli:</div>
                                <div className="border-b border-slate-700 px-2 py-1">{previewData.sendingType || 'KAGIT'}</div>
                                <div className="border-b border-r border-slate-700 px-2 py-1 font-semibold">Düzenleme Tarihi:</div>
                                <div className="border-b border-slate-700 px-2 py-1">{previewData.invoiceDate || '-'}</div>
                                <div className="border-b border-r border-slate-700 px-2 py-1 font-semibold">Düzenleme Zamanı:</div>
                                <div className="border-b border-slate-700 px-2 py-1">00:00:00</div>
                                <div className="border-r border-slate-700 px-2 py-1 font-semibold">Son Ödeme Tarihi:</div>
                                <div className="px-2 py-1">{previewData.invoiceDate || '-'}</div>
                              </div>
                            </div>
                          </div>

                          <div className="mt-6 border-y border-slate-800 py-2 text-[10px] uppercase">
                            <div className="font-semibold">SAYIN</div>
                            <div className="font-semibold">{previewData.receiverName || '-'}</div>
                            <div>{previewData.receiverCity ? `${previewData.receiverCity} ${previewData.receiverPostalCode || ''}` : '-'}</div>
                            <div>Vergi Dairesi: {previewData.receiverTaxOffice || '-'}</div>
                            <div>{previewData.receiverTaxCode?.length === 11 ? 'TCKN' : 'VKN'}: {previewData.receiverTaxCode || '-'}</div>
                          </div>

                          <div className="mt-2 text-[10px]">
                            <b>ETTN:</b> -
                          </div>

                          <div className="mt-3 border border-slate-700">
                            <div className="grid grid-cols-[36px_1.2fr_1.2fr_0.8fr_0.9fr_0.6fr_0.7fr_0.6fr_0.7fr_0.9fr_1fr] border-b border-slate-700 bg-slate-50 text-[9px] font-semibold">
                              <div className="border-r border-slate-700 px-1 py-1">Sıra No</div>
                              <div className="border-r border-slate-700 px-1 py-1">Mal Hizmet</div>
                              <div className="border-r border-slate-700 px-1 py-1">Açıklama</div>
                              <div className="border-r border-slate-700 px-1 py-1">Miktar</div>
                              <div className="border-r border-slate-700 px-1 py-1">Birim Fiyat</div>
                              <div className="border-r border-slate-700 px-1 py-1">İskonto Oranı</div>
                              <div className="border-r border-slate-700 px-1 py-1">İskonto Tutarı</div>
                              <div className="border-r border-slate-700 px-1 py-1">KDV Oranı</div>
                              <div className="border-r border-slate-700 px-1 py-1">KDV Tutarı</div>
                              <div className="border-r border-slate-700 px-1 py-1">Diğer Vergiler</div>
                              <div className="px-1 py-1 text-right">Mal Hizmet Tutarı</div>
                            </div>
                            {previewData.details.length === 0 ? (
                              <div className="px-2 py-3 text-center text-[10px] text-slate-500">Kalem bulunamadı</div>
                            ) : previewData.details.map((line, idx) => (
                              <div key={`${line.name}-${idx}`} className="grid grid-cols-[36px_1.2fr_1.2fr_0.8fr_0.9fr_0.6fr_0.7fr_0.6fr_0.7fr_0.9fr_1fr] border-b border-slate-700 text-[9px]">
                                <div className="border-r border-slate-700 px-1 py-1">{idx + 1}</div>
                                <div className="border-r border-slate-700 px-1 py-1">{line.name || '-'}</div>
                                <div className="border-r border-slate-700 px-1 py-1"></div>
                                <div className="border-r border-slate-700 px-1 py-1">{formatQty(line.quantity)}</div>
                                <div className="border-r border-slate-700 px-1 py-1">{formatMoney(line.unitPrice)}</div>
                                <div className="border-r border-slate-700 px-1 py-1">%{formatQty(line.discountRate)}</div>
                                <div className="border-r border-slate-700 px-1 py-1">{formatMoney(line.discountAmount)}</div>
                                <div className="border-r border-slate-700 px-1 py-1">%{formatQty(line.vatRate)}</div>
                                <div className="border-r border-slate-700 px-1 py-1">{formatMoney(line.vatAmount)}</div>
                                <div className="border-r border-slate-700 px-1 py-1">{formatMoney(line.otherTaxes)}</div>
                                <div className="px-1 py-1 text-right">{formatMoney(line.lineTotal)}</div>
                              </div>
                            ))}
                          </div>

                          <div className="mt-2 flex justify-end">
                            <div className="w-full max-w-[320px] border border-slate-700 text-[10px]">
                              <div className="grid grid-cols-[1fr_120px] border-b border-slate-700">
                                <div className="border-r border-slate-700 px-2 py-1 font-semibold">Mal Hizmet Toplam Tutarı</div>
                                <div className="px-2 py-1 text-right">{formatMoney(previewData.totalLineExtension)}</div>
                              </div>
                              <div className="grid grid-cols-[1fr_120px] border-b border-slate-700">
                                <div className="border-r border-slate-700 px-2 py-1 font-semibold">Toplam İskonto</div>
                                <div className="px-2 py-1 text-right">{formatMoney(previewData.totalDiscount)}</div>
                              </div>
                              <div className="grid grid-cols-[1fr_120px] border-b border-slate-700">
                                <div className="border-r border-slate-700 px-2 py-1 font-semibold">KDV Matrahı %0.00</div>
                                <div className="px-2 py-1 text-right">
                                  {formatMoney(previewData.details.filter(x => toNumber(x.vatRate) === 0).reduce((acc, cur) => acc + toNumber(cur.lineTotal), 0))}
                                </div>
                              </div>
                              <div className="grid grid-cols-[1fr_120px] border-b border-slate-700">
                                <div className="border-r border-slate-700 px-2 py-1 font-semibold">KDV Matrahı %20.00</div>
                                <div className="px-2 py-1 text-right">
                                  {formatMoney(previewData.details.filter(x => toNumber(x.vatRate) !== 0).reduce((acc, cur) => acc + toNumber(cur.lineTotal), 0))}
                                </div>
                              </div>
                              <div className="grid grid-cols-[1fr_120px] border-b border-slate-700">
                                <div className="border-r border-slate-700 px-2 py-1 font-semibold">Hesaplanan KDV(%0.00)</div>
                                <div className="px-2 py-1 text-right">{formatMoney(0)}</div>
                              </div>
                              <div className="grid grid-cols-[1fr_120px] border-b border-slate-700">
                                <div className="border-r border-slate-700 px-2 py-1 font-semibold">Hesaplanan KDV(%20.00)</div>
                                <div className="px-2 py-1 text-right">{formatMoney(previewData.totalVat)}</div>
                              </div>
                              <div className="grid grid-cols-[1fr_120px] border-b border-slate-700">
                                <div className="border-r border-slate-700 px-2 py-1 font-semibold">Vergiler Dahil Toplam Tutar</div>
                                <div className="px-2 py-1 text-right">{formatMoney(previewData.totalTaxInclusive)}</div>
                              </div>
                              <div className="grid grid-cols-[1fr_120px]">
                                <div className="border-r border-slate-700 px-2 py-1 font-semibold">Ödenecek Tutar</div>
                                <div className="px-2 py-1 text-right">{formatMoney(previewData.totalPayable)}</div>
                              </div>
                            </div>
                          </div>

                          <div className="mt-3 border border-slate-700 px-2 py-1 text-[10px]">
                            <div><b>Vergi İstisna Muafiyet Sebebi:</b> 351 -</div>
                            <div><b>Not:</b> Yalnız {formatMoney(previewData.totalPayable).replace(' TL', '')} TL&apos;dir</div>
                          </div>
                        </div>
                        </div>
                      )}
                      {xmlView === 'preview' && xmlPreview && !previewData && !xmlLoading && (
                        <div className="absolute left-4 top-4 rounded border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700">
                          Önizleme hazırlanamadı.
                        </div>
                      )}
                      {xmlView === 'xml' && xmlPreview && (
                        <div className="absolute inset-0 overflow-auto bg-slate-900 text-slate-100">
                          <pre className="min-h-full whitespace-pre-wrap break-words p-4 text-xs leading-relaxed">
                            {formattedXml}
                          </pre>
                        </div>
                      )}
                    </div>
                    {xmlSendResult && (
                      <div className={`mx-4 rounded border px-3 py-2 text-sm ${xmlSendResultKind === 'error' ? 'border-rose-200 bg-rose-50 text-rose-700' : 'border-slate-200 bg-slate-50'}`}>
                        {xmlSendResult}
                      </div>
                    )}
                    <div className="shrink-0 border-t border-slate-800 bg-slate-950 p-3 flex items-center justify-end gap-2">
                      <Button
                        variant="outline"
                        onClick={() => setXmlView(v => v === 'preview' ? 'xml' : 'preview')}
                        disabled={!xmlPreview}
                      >
                        {xmlView === 'preview' ? 'XML Görüntüle' : 'Önizlemeye Dön'}
                      </Button>
                      <Button variant="outline" onClick={() => setXmlOpen(false)}>Kapat</Button>
                      <Button
                        onClick={async () => {
                          if (!selected) return
                          await sendTurmob(selected)
                        }}
                        disabled={xmlSendLoading || !xmlPreview}
                      >
                        {xmlSendLoading ? 'Gönderiliyor...' : 'Onayla ve Gönder'}
                      </Button>
                    </div>
                  </div>
                </DialogContent>
              </Dialog>
              <Dialog open={summaryOpen} onOpenChange={setSummaryOpen}>
                <DialogContent className="max-w-md">
                  <DialogHeader>
                    <DialogTitle>Müşteri Özeti</DialogTitle>
                  </DialogHeader>
                  {summaryCustomer && (
                    <div className="space-y-2 text-sm">
                      <div>İsim Soyisim: <b>{summaryCustomer.musteriAdSoyad || '-'}</b></div>
                      <div>T.C. Kimlik No: <b>{summaryCustomer.tckn || '-'}</b></div>
                      <div>VKN: <b>{summaryCustomer.vknNo || '-'}</b></div>
                      <div>Şirket Adı: <b>{summaryCustomer.companyName || '-'}</b></div>
                    </div>
                  )}
                </DialogContent>
              </Dialog>

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
