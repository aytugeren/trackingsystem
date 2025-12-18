"use client"
import { Suspense, useEffect, useMemo, useState } from 'react'
import TouchField from '../../../components/ui/TouchField'
import BigKeypad from '../../../components/ui/BigKeypad'
import SuccessToast from '../../../components/ui/SuccessToast'
import ErrorToast from '../../../components/ui/ErrorToast'
import { useOfflineQueue } from '../../../hooks/useOfflineQueue'
import BackButton from '../../../components/BackButton'
import { useSearchParams } from 'next/navigation'
import { authHeaders } from '../../../lib/api'

type TxType = 'invoice' | 'expense'
type CustomerSuggestion = { id: string; adSoyad: string; tckn: string; phone?: string | null; email?: string | null; hasContact?: boolean }

function todayStr() {
  const d = new Date()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${d.getFullYear()}-${m}-${day}`
}

function splitFullName(name: string): { firstName: string; lastName: string } {
  const parts = (name || '').trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return { firstName: '', lastName: '' }
  if (parts.length === 1) return { firstName: parts[0], lastName: '' }
  const firstName = parts[0]
  const lastName = parts.slice(1).join(' ')
  return { firstName, lastName }
}

function InvoiceNewInner() {
  const searchParams = useSearchParams()
  const initialType = (searchParams.get('type') === 'expense' ? 'expense' : (searchParams.get('type') === 'invoice' ? 'invoice' : 'invoice')) as TxType

  const apiBase = useMemo(() => process.env.NEXT_PUBLIC_API_URL || '', [])
  const { sendOrQueue, isOnline } = useOfflineQueue(apiBase)

  const [txType, setTxType] = useState<TxType>(initialType)
  const [date, setDate] = useState<string>(todayStr())
  const [fullName, setFullName] = useState<string>('')
  const [birthYear, setBirthYear] = useState<string>(() => {
    const now = new Date().getFullYear()
    const min = now - 85
    const max = now - 18
    const r = Math.floor(Math.random() * (max - min + 1)) + min
    return String(r)
  })
  const [tckn, setTckn] = useState<string>('')
  const [customerSuggestions, setCustomerSuggestions] = useState<CustomerSuggestion[]>([])
  const [suggestFor, setSuggestFor] = useState<'name' | 'tckn' | null>(null)
  const [phone, setPhone] = useState<string>('')
  const [email, setEmail] = useState<string>('')
  const [needsContact, setNeedsContact] = useState<boolean>(false)
  const [existingCustomerId, setExistingCustomerId] = useState<string | null>(null)
  const [amount, setAmount] = useState<string>('')
  const [payment, setPayment] = useState<'havale' | 'krediKarti' | ''>('')
  const [loading, setLoading] = useState(false)
  const [success, setSuccess] = useState('')
  const [error, setError] = useState('')
  const [activeKeypad, setActiveKeypad] = useState<'amount' | null>(null)
  const [tcknError, setTcknError] = useState<string>('')
  const [ayar, setAyar] = useState<22 | 24 | null>(null)
  const [previewOpen, setPreviewOpen] = useState(false)
  const [predictedSira, setPredictedSira] = useState<number | null>(null)
  const [currentAltinSatis, setCurrentAltinSatis] = useState<number | null>(null)
  const [savedHasAltin, setSavedHasAltin] = useState<number | null>(null)
  const [hasAltinInput, setHasAltinInput] = useState<string>('')
  const [hasAltinUpdatedAt, setHasAltinUpdatedAt] = useState<string | null>(null)
  const [hasAltinUpdatedBy, setHasAltinUpdatedBy] = useState<string | null>(null)
  const [hasAltinLoading, setHasAltinLoading] = useState(false)
  const [hasAltinSaving, setHasAltinSaving] = useState(false)
  const [hasAltinEditing, setHasAltinEditing] = useState(false)
  const [draftId, setDraftId] = useState<string | null>(null)
  const [copied, setCopied] = useState<string | null>(null)
  const PENDING_DRAFT_KEY = 'cashierPendingDraft'
  async function copy(key: string, text: string) {
    try {
      await navigator.clipboard.writeText(text)
      setCopied(key)
      setTimeout(() => setCopied(null), 1500)
    } catch {}
  }
  const CopyIcon = (props: React.SVGProps<SVGSVGElement>) => (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} width={14} height={14} {...props}>
      <rect x="9" y="9" width="10" height="10" rx="2" ry="2" />
      <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1" />
    </svg>
  )
  const CheckIcon = (props: React.SVGProps<SVGSVGElement>) => (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} width={14} height={14} {...props}>
      <path d="M20 6 9 17l-5-5" />
    </svg>
  )

  function validateTCKN(id: string): boolean {
    if (!/^\d{11}$/.test(id)) return false
    if (id[0] === '0') return false
    const digits = id.split('').map((d) => parseInt(d, 10))
    const d1 = digits[0], d2 = digits[1], d3 = digits[2], d4 = digits[3], d5 = digits[4], d6 = digits[5], d7 = digits[6], d8 = digits[7], d9 = digits[8], d10 = digits[9], d11 = digits[10]
    const calc10 = (((d1 + d3 + d5 + d7 + d9) * 7) - (d2 + d4 + d6 + d8)) % 10
    const calc11 = (digits.slice(0, 10).reduce((a, b) => a + b, 0)) % 10
    return d10 === calc10 && d11 === calc11
  }

  function computeBirthYear(): number {
    const now = new Date().getFullYear()
    const min = now - 85
    const max = now - 18
    if (birthYear && /^\d{4}$/.test(birthYear)) {
      const val = parseInt(birthYear, 10)
      if (val >= 1900 && val <= now) return val
    }
    return Math.floor(Math.random() * (max - min + 1)) + min
  }

  async function loadHasAltin() {
    setHasAltinLoading(true)
    try {
      const res = await fetch(apiBase + '/api/pricing/gold', { cache: 'no-store', headers: { ...authHeaders() } })
      if (res.status === 404) {
        setHasAltinInput('')
        setHasAltinUpdatedAt(null)
        setHasAltinUpdatedBy(null)
        setSavedHasAltin(null)
        setCurrentAltinSatis(null)
        return
      }
      if (!res.ok) throw new Error('cannot load')
      const j = await res.json()
      const priceVal = Number(j?.price ?? 0)
      setHasAltinInput(priceVal ? formatHasAltinDisplay(String(priceVal).replace('.', ',')) : '')
      setHasAltinUpdatedAt(j?.updatedAt ?? null)
      setHasAltinUpdatedBy(j?.updatedBy ?? null)
      setSavedHasAltin(Number.isFinite(priceVal) ? priceVal : null)
      setCurrentAltinSatis(Number.isFinite(priceVal) ? priceVal : null)
    } catch {
      setError('Has Altin bilgisi alinmadi')
    } finally {
      setHasAltinLoading(false)
    }
  }

  async function saveHasAltin(showToast = true): Promise<boolean> {
    const parsed = parseHasAltinValue()
    if (!parsed || Number.isNaN(parsed)) { setError('Gecerli bir Has Altin fiyati girin'); return false }
    if (showToast) { setError(''); setSuccess('') }
    setHasAltinSaving(true)
    try {
      const res = await fetch(apiBase + '/api/pricing/gold', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json', ...authHeaders() },
        body: JSON.stringify({ price: parsed })
      })
      if (!res.ok) throw new Error('kaydedilemedi')
      const j = await res.json()
      const priceVal = Number(j?.price ?? parsed)
      setHasAltinInput(priceVal ? formatHasAltinDisplay(String(priceVal).replace('.', ',')) : '')
      setHasAltinUpdatedAt(j?.updatedAt ?? new Date().toISOString())
      setHasAltinUpdatedBy(j?.updatedBy ?? null)
      setSavedHasAltin(Number.isFinite(priceVal) ? priceVal : parsed)
      setCurrentAltinSatis(Number.isFinite(priceVal) ? priceVal : parsed)
      if (showToast) setSuccess('Has Altin guncellendi')
      setHasAltinEditing(false)
      return true
    } catch {
      if (showToast) setError('Has Altin guncellenemedi')
      return false
    } finally {
      setHasAltinSaving(false)
    }
  }

  async function openPreview() {
    setError('')
    const hasVal = parseHasAltinValue()
    if (!hasVal || Number.isNaN(hasVal)) { setError('Has Altın fiyatı gerekli'); return }
    if (savedHasAltin == null || Math.abs(hasVal - savedHasAltin) > 0.0005) {
      const saved = await saveHasAltin(false)
      if (!saved) { setError('Has Altın fiyatı kaydedilemedi'); return }
    }
    if (!date || !fullName || !tckn || !amount || (txType === 'invoice' && !payment) || !ayar) {
      setError('Lütfen tüm alanları doldurun')
      return
    }
    if (!validateTCKN(tckn)) { setError('TCKN geçersiz'); return }
    // doğum yılı kontrolü kaldırıldı
    const amountNum = parseFloat(amount.replace(',', '.'))
    if (Number.isNaN(amountNum)) { setError('Tutar geçersiz'); return }

    if (!isOnline) { setError('Önizleme için çevrimiçi olmalısınız'); return }
    try {
      const headers = { 'Content-Type': 'application/json', ...authHeaders() }
      const payload: any = {
        tarih: date,
        siraNo: 0,
        musteriAdSoyad: fullName.trim(),
        tckn: tckn,
        tutar: amountNum,
        altinAyar: ayar,
        telefon: phone.trim() || undefined,
        email: email.trim() || undefined
      }
      if (txType === 'invoice') payload.odemeSekli = payment === 'havale' ? 0 : 1
      const url = txType === 'invoice' ? '/api/cashier/invoices/draft' : '/api/cashier/expenses/draft'
      const r = await fetch(apiBase + url, { method: 'POST', headers, body: JSON.stringify(payload) })
      if (!r.ok) { setError('Önizleme oluşturulamadı'); return }
      const j = await r.json()
      setDraftId(String(j?.id))
      try {
        const newId = String(j?.id)
        if (newId) {
          localStorage.setItem(PENDING_DRAFT_KEY, JSON.stringify({ id: newId, type: txType }))
        }
      } catch {}
      setPredictedSira(Number(j?.siraNo || 0))
      setCurrentAltinSatis(j?.altinSatisFiyati != null ? Number(j.altinSatisFiyati) : null)
    } catch {
      setError('Önizleme oluşturulamadı');
      return
    }
    setPreviewOpen(true)
  }

  useEffect(() => {
    loadHasAltin()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useEffect(() => {
    const source = suggestFor
    const query = source === 'name' ? fullName : (source === 'tckn' ? tckn : '')
    if (!source || !query || query.trim().length < 2 || !isOnline) {
      setCustomerSuggestions([])
      return
    }
    const ctrl = new AbortController()
    const timer = setTimeout(async () => {
      try {
        const res = await fetch(`${apiBase}/api/customers/suggest?q=${encodeURIComponent(query.trim())}&limit=8`, {
          headers: { ...authHeaders() },
          signal: ctrl.signal
        })
        if (!res.ok) { setCustomerSuggestions([]); return }
        const data = await res.json()
        const items = Array.isArray(data) ? data : (Array.isArray((data as any)?.items) ? (data as any).items : [])
        const mapped = (items as any[]).map(it => {
          const phoneVal = it.phone ?? it.Phone ?? null
          const emailVal = it.email ?? it.Email ?? null
          const hasContact = (it.hasContact ?? it.has_contact ?? it.has_contact_info ?? null)
          return {
            id: String(it.id ?? it.Id ?? ''),
            adSoyad: String(it.adSoyad ?? it.AdSoyad ?? it.name ?? ''),
            tckn: String(it.tckn ?? it.TCKN ?? ''),
            phone: phoneVal != null ? String(phoneVal) : null,
            email: emailVal != null ? String(emailVal) : null,
            hasContact: hasContact != null ? Boolean(hasContact) : Boolean(phoneVal) || Boolean(emailVal)
          }
        }).filter(x => x.id)
        setCustomerSuggestions(mapped)
      } catch (err: any) {
        if (err?.name === 'AbortError') return
        setCustomerSuggestions([])
      }
    }, 200)
    return () => { ctrl.abort(); clearTimeout(timer) }
  }, [suggestFor, fullName, tckn, apiBase, isOnline])

  const formattedHasAltin = savedHasAltin != null
    ? Number(savedHasAltin).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY', maximumFractionDigits: 3 })
    : 'Has Altin girilmedi'
  const formattedHasAltinMeta = hasAltinUpdatedAt
    ? `${new Date(hasAltinUpdatedAt).toLocaleString('tr-TR')}${hasAltinUpdatedBy ? ` · ${hasAltinUpdatedBy}` : ''}`
    : (hasAltinUpdatedBy ? `Güncelleyen: ${hasAltinUpdatedBy}` : 'Henüz güncellenmedi')
  const hasAltinMissing = savedHasAltin == null

  // On mount: if a pending draft exists (from a refresh), delete it
  useEffect(() => {
    try {
      const raw = localStorage.getItem(PENDING_DRAFT_KEY)
      if (!raw) return
      const parsed = JSON.parse(raw) as { id: string; type: TxType }
      if (parsed?.id && parsed?.type) {
        const delUrl = `${apiBase}/${parsed.type === 'invoice' ? 'api/invoices' : 'api/expenses'}/${parsed.id}`
        fetch(delUrl, { method: 'DELETE', headers: { 'Content-Type': 'application/json', ...authHeaders() } })
          .finally(() => { try { localStorage.removeItem(PENDING_DRAFT_KEY) } catch {} })
      } else {
        localStorage.removeItem(PENDING_DRAFT_KEY)
      }
    } catch {}
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // When preview closes or draft clears, ensure local pending key is removed
  useEffect(() => {
    if (!previewOpen || !draftId) {
      try { localStorage.removeItem(PENDING_DRAFT_KEY) } catch {}
    }
  }, [previewOpen, draftId])

  // Extra assurance on refresh/close: try to delete draft on unload
  useEffect(() => {
    if (!previewOpen || !draftId) return
    const type: TxType = txType
    const id = draftId
    const delUrl = `${apiBase}/${type === 'invoice' ? 'api/invoices' : 'api/expenses'}/${id}`

    const handler = () => {
      // Avoid racing with confirmation in progress
      if (loading) return
      try {
        localStorage.setItem(PENDING_DRAFT_KEY, JSON.stringify({ id, type }))
      } catch {}
      try {
        // Best-effort delete with keepalive so it can dispatch while unloading
        fetch(delUrl, { method: 'DELETE', keepalive: true, headers: { 'Content-Type': 'application/json', ...authHeaders() } }).catch(() => {})
      } catch {}
    }

    window.addEventListener('beforeunload', handler)
    window.addEventListener('pagehide', handler)

    return () => {
      window.removeEventListener('beforeunload', handler)
      window.removeEventListener('pagehide', handler)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [previewOpen, draftId, loading])

  
  const handleKeypadTo = (setter: React.Dispatch<React.SetStateAction<string>>) => ({
    onKey: (k: string) => setter((v) => sanitizeAmountInput(v + k)),
    onBackspace: () => setter((v) => v.slice(0, -1)),
    onClear: () => setter('')
  })

  function sanitizeAmountInput(v: string): string {
    const cleaned = v.replace(/\./g, '').replace(/[^0-9,]/g, '')
    const firstComma = cleaned.indexOf(',')
    if (firstComma === -1) return cleaned
    const intPart = cleaned.slice(0, firstComma).replace(/,/g, '')
    const fracPart = cleaned.slice(firstComma + 1).replace(/,/g, '').slice(0, 2)
    return intPart + (fracPart ? (',' + fracPart) : ',')
  }

  function formatAmountDisplay(v: string): string {
    if (!v) return ''
    const normalized = sanitizeAmountInput(v)
    const [intPart, fracPart] = normalized.split(',')
    const withSep = intPart.replace(/\B(?=(\d{3})+(?!\d))/g, '.')
    return fracPart != null && fracPart !== '' ? `${withSep},${fracPart}` : (normalized.endsWith(',') ? withSep + ',' : withSep)
  }

  function sanitizeHasAltinInput(v: string): string {
    const cleaned = v.replace(/\./g, '').replace(/[^0-9,]/g, '')
    const firstComma = cleaned.indexOf(',')
    if (firstComma === -1) return cleaned
    const intPart = cleaned.slice(0, firstComma).replace(/,/g, '')
    const fracPart = cleaned.slice(firstComma + 1).replace(/,/g, '').slice(0, 3)
    return intPart + (fracPart ? (',' + fracPart) : ',')
  }

  function formatHasAltinDisplay(v: string): string {
    if (!v) return ''
    const normalized = sanitizeHasAltinInput(v)
    const [intPart, fracPart] = normalized.split(',')
    const withSep = intPart.replace(/\B(?=(\d{3})+(?!\d))/g, '.')
    if (fracPart != null) {
      const limited = fracPart.slice(0, 3)
      return limited !== '' ? `${withSep},${limited}` : (normalized.endsWith(',') ? withSep + ',' : withSep)
    }
    return normalized.endsWith(',') ? withSep + ',' : withSep
  }

  function parseHasAltinValue(): number {
    return parseFloat((sanitizeHasAltinInput(hasAltinInput || '0') || '0').replace(',', '.'))
  }

  function applyCustomerSuggestion(s: CustomerSuggestion) {
    setFullName(s.adSoyad || '')
    setTckn(String(s.tckn || ''))
    setTcknError('')
    setExistingCustomerId(s.id || null)
    setPhone(s.phone ? String(s.phone) : '')
    setEmail(s.email ? String(s.email) : '')
    setNeedsContact(!s.hasContact)
    setCustomerSuggestions([])
    setSuggestFor(null)
  }

  function renderSuggestions(field: 'name' | 'tckn') {
    if (suggestFor !== field || customerSuggestions.length === 0) return null
    return (
      <div style={{ position: 'absolute', zIndex: 30, left: 0, right: 0, top: '100%', marginTop: 4, background: '#fff', border: '1px solid #eee', borderRadius: 8, boxShadow: '0 10px 26px rgba(0,0,0,0.12)', maxHeight: 240, overflowY: 'auto' }}>
        {customerSuggestions.map((s) => (
          <button
            key={s.id}
            type="button"
            onClick={() => applyCustomerSuggestion(s)}
            style={{ width: '100%', padding: '10px 12px', textAlign: 'left', background: 'white', border: 'none', borderBottom: '1px solid #f1f1f1', cursor: 'pointer' }}
          >
            <div style={{ fontWeight: 700, fontSize: 14, color: '#111' }}>{s.adSoyad || '-'}</div>
            <div style={{ fontSize: 12, color: '#666', marginTop: 2 }}>TCKN: {s.tckn || '-'}</div>
          </button>
        ))}
      </div>
    )
  }

  async function onSave() {
    setError('')
    setSuccess('')
    if (!date || !fullName || !tckn || !amount || (txType === 'invoice' && !payment) || !ayar) {
      setError('Lütfen tüm alanları doldurun')
      return
    }
    if (!validateTCKN(tckn)) {
      setError('TCKN geçersiz')
      return
    }
    // doğum yılı kontrolü kaldırıldı
    const amountNum = parseFloat(amount.replace(',', '.'))
    if (Number.isNaN(amountNum)) {
      setError('Tutar geçersiz')
      return
    }
    const hasVal = parseHasAltinValue()
    if (!hasVal || Number.isNaN(hasVal) || savedHasAltin == null) {
      setError('Has Altın fiyatı gerekli')
      return
    }
    setLoading(true)

    const verifyRequired = (process.env.NEXT_PUBLIC_ID_VERIFY_REQUIRED || 'false') === 'true'
    const verifyPath = process.env.NEXT_PUBLIC_ID_VERIFY_URL || '/api/identity/verify'
    try {
      if (verifyPath && (process.env.NEXT_PUBLIC_ID_VERIFY_ENABLED || 'false') === 'true') {
        const { firstName, lastName } = splitFullName(fullName)
        const res = await fetch(apiBase + verifyPath, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ tckn, firstName, lastName, birthYear: computeBirthYear() })
        })
        if (!res.ok) {
          if (verifyRequired) {
            setLoading(false)
            setError('Resmi kimlik doğrulaması başarısız')
            return
          }
        } else {
          try {
            const data = await res.json()
            if (verifyRequired && !data?.verified) {
              setLoading(false)
              setError('Resmi kimlik doğrulaması olumsuz')
              return
            }
          } catch {}
        }
      }
    } catch {
      if (verifyRequired) {
        setLoading(false)
        setError('Resmi doğrulama için bağlantı gerekli')
        return
      }
    }

    const { firstName, lastName } = splitFullName(fullName)
  const common: any = {
    tarih: date,
    siraNo: 0,
    musteriAdSoyad: fullName.trim(),
    tckn: tckn,
    tutar: amountNum,
    fullName: fullName.trim(),
    firstName,
    lastName,
    birthYear: computeBirthYear(),
    altinAyar: ayar,
    telefon: phone.trim() || undefined,
    email: email.trim() || undefined
  }

    // If draft exists, finalize it; else fallback to original create
    let res: any = null
    if (draftId) {
      try {
        const finalizeUrl = txType === 'invoice' ? `/api/invoices/${draftId}/finalize` : `/api/expenses/${draftId}/finalize`
        const r = await fetch(apiBase + finalizeUrl, { method: 'POST', headers: { 'Content-Type': 'application/json', ...authHeaders() } })
        res = r.ok ? { ok: true } : { queued: false }
      } catch { res = { queued: false } }
    } else {
      let endpoint = ''
      let payload: any = { ...common }
      if (txType === 'invoice') {
        endpoint = '/api/invoices'
        payload.odemeSekli = payment === 'havale' ? 0 : 1
      } else {
        endpoint = '/api/expenses'
      }
      res = await sendOrQueue(endpoint, payload)
    }
    setLoading(false)
    if ((res as any).ok) {
      setSuccess('Kayıt eklendi ✅')
      // Kredi Kartı tahsilat kuyruğu bilgisini bildir
      if (txType === 'invoice' && payment === 'krediKarti') {
        try {
          const listRes = await fetch(`${apiBase}/api/invoices?page=1&pageSize=500`, {
            method: 'GET',
            headers: {
              'Content-Type': 'application/json',
              ...authHeaders()
            }
          })
          if (listRes.ok) {
            const data = await listRes.json()
            const items = (data?.items || []) as Array<{ tarih: string; siraNo: number }>
            const targetDate = date
            const todays = items.filter(x => String(x.tarih) === targetDate)
            const maxSira = todays.reduce((m, it) => Math.max(m, it.siraNo || 0), 0)
            if (maxSira > 0) {
              setSuccess(`Kredi Kartı kuyruğu sıra numaranız: ${maxSira}`)
            }
          }
        } catch {}
      }
      setCustomerSuggestions([]); setSuggestFor(null)
      setCustomerSuggestions([]); setSuggestFor(null)
      setFullName(''); setBirthYear(''); setTckn(''); setAmount(''); setPayment(''); setAyar(null); setPreviewOpen(false); setPredictedSira(null); setDraftId(null); setPhone(''); setEmail(''); setNeedsContact(false)
    } else if ((res as any).queued) {
      setSuccess('Çevrimdışı kaydedildi, sonra gönderilecek ✅')
      setFullName(''); setBirthYear(''); setTckn(''); setAmount(''); setPayment(''); setAyar(null); setPreviewOpen(false); setPredictedSira(null); setDraftId(null); setPhone(''); setEmail(''); setNeedsContact(false)
    } else {
      setError('Kayıt eklenemedi')
    }
  }

  return (
    <main>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12 }}>
        <h1>{txType === 'invoice' ? 'Fatura / Gider - Fatura' : 'Fatura / Gider - Gider'}</h1>
        <BackButton />
      </div>

      <div className="card" style={{ marginTop: 8, marginBottom: 8, display: 'flex', flexDirection: 'column', gap: 10 }}>
        {hasAltinLoading ? (
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <div className="spinner" />
            <div>Yukleniyor...</div>
          </div>
        ) : hasAltinEditing ? (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <div style={{ fontWeight: 700, fontSize: 12 }}>Has Altin (Global)</div>
            <TouchField
              label="Has Altin (TL)"
              value={formatHasAltinDisplay(hasAltinInput)}
              onChange={(v) => setHasAltinInput(sanitizeHasAltinInput(v))}
              inputMode="decimal"
              onFocus={() => setActiveKeypad(null)}
            />
            <div style={{ display: 'flex', gap: 8 }}>
              <button className="primary" onClick={() => saveHasAltin(true)} disabled={hasAltinSaving}>{hasAltinSaving ? 'Kaydediliyor...' : 'Kaydet'}</button>
              <button className="secondary" onClick={() => setHasAltinEditing(false)} disabled={hasAltinSaving}>Vazgec</button>
            </div>
          </div>
        ) : (
          <button
            onClick={() => setHasAltinEditing(true)}
            style={{ background: 'none', border: 'none', textAlign: 'left', padding: 0, cursor: 'pointer', display: 'flex', flexDirection: 'column', gap: 6 }}
          >
            <div style={{ fontWeight: 700, color: '#111' }}>Has Altin (Global)</div>
            <div style={{ fontSize: 20, fontWeight: 800, color: '#111' }}>{formattedHasAltin}</div>
          </button>
        )}
        <div style={{ fontSize: 11, color: '#666' }}>{formattedHasAltinMeta}</div>
      </div>

      {hasAltinMissing && (
        <div className="card" style={{ background: '#fdecea', color: '#b3261e', border: '1px solid #f5c2c0', marginBottom: 12 }}>
          Lutfen has altin giriniz. Has altin olmadan islem yapamazsiniz.
        </div>
      )}

      <div style={{ display: 'flex', gap: 12, marginBottom: 12 }}>
        <button type="button" className={txType === 'invoice' ? 'primary' : 'secondary'} onClick={() => setTxType('invoice')} style={{ flex: 1 }} disabled={hasAltinMissing}>Fatura</button>
        <button type="button" className={txType === 'expense' ? 'primary' : 'secondary'} onClick={() => setTxType('expense')} style={{ flex: 1 }} disabled={hasAltinMissing}>Gider</button>
      </div>

      <div className="card" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        <TouchField label="Tarih" type="date" value={date} onChange={setDate} disabled={hasAltinMissing} />
        <div style={{ position: 'relative' }}>
          <TouchField label="Ad Soyad" value={fullName} onChange={(v) => { setFullName(v); setSuggestFor('name') }} onFocus={() => { setActiveKeypad(null); setSuggestFor('name') }} upperCaseTr disabled={hasAltinMissing} />
          {renderSuggestions('name')}
        </div>
        {/* Doğum yılı alanı kaldırıldı */}
        <div style={{ position: 'relative' }}>
          <TouchField label="TCKN" value={tckn} onChange={(v) => { const cleaned = v.replace(/[^0-9]/g, '').slice(0, 11); setTckn(cleaned); setTcknError(''); setSuggestFor('tckn'); setNeedsContact(false); setExistingCustomerId(null); setPhone(''); setEmail(''); }} inputMode="numeric" pattern="\\d*" maxLength={11} onFocus={() => { setActiveKeypad(null); setSuggestFor('tckn') }} error={tcknError} disabled={hasAltinMissing} />
          {renderSuggestions('tckn')}
        </div>
        {existingCustomerId && (
          <>
            {needsContact && (
              <div className="card" style={{ background: '#fff7ed', border: '1px solid #fed7aa', color: '#9a3412' }}>
                Daha önce gelen müşteri için telefon veya e-posta alınmamış. Lütfen ekleyin (zorunlu değil).
              </div>
            )}
            <TouchField label="Telefon (isteğe bağlı)" value={phone} onChange={(v) => setPhone(v.replace(/[^0-9+]/g, '').slice(0, 20))} inputMode="tel" onFocus={() => setActiveKeypad(null)} disabled={hasAltinMissing} />
            <TouchField label="Email (isteğe bağlı)" value={email} onChange={(v) => setEmail(v.trim())} inputMode="email" onFocus={() => setActiveKeypad(null)} disabled={hasAltinMissing} />
          </>
        )}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          <TouchField label="Tutar" value={formatAmountDisplay(amount)} onChange={(v) => setAmount(sanitizeAmountInput(v))} inputMode="decimal" onFocus={() => !hasAltinMissing && setActiveKeypad('amount')} disabled={hasAltinMissing} />
          {activeKeypad === 'amount' && !hasAltinMissing && (
            <BigKeypad {...handleKeypadTo(setAmount)} />
          )}
        </div>

        <div style={{ display: 'flex', gap: 12 }}>
          <button type="button" className={ayar === 22 ? 'primary' : 'secondary'} onClick={() => setAyar(22)} style={{ flex: 1 }} disabled={hasAltinMissing}>22 Ayar</button>
          <button type="button" className={ayar === 24 ? 'primary' : 'secondary'} onClick={() => setAyar(24)} style={{ flex: 1 }} disabled={hasAltinMissing}>24 Ayar</button>
        </div>

        {txType === 'invoice' && (
          <div style={{ display: 'flex', gap: 12 }}>
            <button type="button" className={payment === 'havale' ? 'primary' : 'secondary'} onClick={() => setPayment('havale')} style={{ flex: 1 }} disabled={hasAltinMissing}>Havale</button>
            <button type="button" className={payment === 'krediKarti' ? 'primary' : 'secondary'} onClick={() => setPayment('krediKarti')} style={{ flex: 1 }} disabled={hasAltinMissing}>Kredi Kartı</button>
          </div>
        )}

        <div className="actions">
          <button className="primary" onClick={openPreview} disabled={loading || hasAltinMissing}>
            {loading ? 'Hazırlanıyor…' : 'Önizle'}
          </button>
        </div>
      </div>
      {previewOpen && (
        <div className="modal-overlay" style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 50 }}>
          <div className="modal" style={{ background: '#fff', borderRadius: 8, padding: 16, width: '100%', maxWidth: 600 }}>
            <h2 style={{ marginTop: 0 }}>{txType === 'invoice' ? 'Fatura Bilgileri' : 'Gider Bilgileri'}</h2>
            <div style={{ fontSize: 14, color: '#555', marginBottom: 8, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div>Sıra No: <b>{predictedSira ?? '-'}</b></div>
              {predictedSira != null && (
                <button aria-label="Kopyala" title={copied === 'sira' ? 'Kopyalandı' : 'Kopyala'} onClick={() => copy('sira', String(predictedSira))} style={{ background: 'none', border: 'none', padding: 4, cursor: 'pointer' }}>
                  {copied === 'sira' ? <CheckIcon /> : <CopyIcon />}
                </button>
              )}
            </div>
            <div className="card" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
                <div><b>Ad Soyad:</b> {fullName || '-'}</div>
                {fullName ? (
                  <button aria-label="Kopyala" title={copied === 'adsoyad' ? 'Kopyalandı' : 'Kopyala'} onClick={() => copy('adsoyad', fullName)} style={{ background: 'none', border: 'none', padding: 4, cursor: 'pointer' }}>
                    {copied === 'adsoyad' ? <CheckIcon /> : <CopyIcon />}
                  </button>
                ) : null}
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
                <div><b>TCKN:</b> {tckn || '-'}</div>
                {tckn ? (
                  <button aria-label="Kopyala" title={copied === 'tckn' ? 'Kopyalandı' : 'Kopyala'} onClick={() => copy('tckn', tckn)} style={{ background: 'none', border: 'none', padding: 4, cursor: 'pointer' }}>
                    {copied === 'tckn' ? <CheckIcon /> : <CopyIcon />}
                  </button>
                ) : null}
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
                <div><b>Tarih:</b> {date || '-'}</div>
                {date ? (
                  <button aria-label="Kopyala" title={copied === 'tarih' ? 'Kopyalandı' : 'Kopyala'} onClick={() => copy('tarih', date)} style={{ background: 'none', border: 'none', padding: 4, cursor: 'pointer' }}>
                    {copied === 'tarih' ? <CheckIcon /> : <CopyIcon />}
                  </button>
                ) : null}
              </div>
              {txType === 'invoice' && (
                <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
                  <div><b>Ödeme:</b> {payment ? (payment === 'havale' ? 'Havale' : 'Kredi Kartı') : '-'}</div>
                  {payment ? (
                    <button aria-label="Kopyala" title={copied === 'odeme' ? 'Kopyalandı' : 'Kopyala'} onClick={() => copy('odeme', payment === 'havale' ? 'Havale' : 'Kredi Kartı')} style={{ background: 'none', border: 'none', padding: 4, cursor: 'pointer' }}>
                      {copied === 'odeme' ? <CheckIcon /> : <CopyIcon />}
                    </button>
                  ) : null}
                </div>
              )}
              <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
                <div><b>Ayar:</b> {ayar ? (ayar === 22 ? '22 Ayar' : '24 Ayar') : '-'}</div>
                {ayar ? (
                  <button aria-label="Kopyala" title={copied === 'ayar' ? 'Kopyalandı' : 'Kopyala'} onClick={() => copy('ayar', ayar === 22 ? '22 Ayar' : '24 Ayar')} style={{ background: 'none', border: 'none', padding: 4, cursor: 'pointer' }}>
                    {copied === 'ayar' ? <CheckIcon /> : <CopyIcon />}
                  </button>
                ) : null}
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
                {(() => {
                  const display = amount ? Number(parseFloat(amount.replace(',', '.'))).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' }) : '-'
                  return (
                    <>
                      <div><b>Tutar:</b> {display}</div>
                      {amount ? (
                        <button aria-label="Kopyala" title={copied === 'tutar' ? 'Kopyalandı' : 'Kopyala'} onClick={() => copy('tutar', display)} style={{ background: 'none', border: 'none', padding: 4, cursor: 'pointer' }}>
                          {copied === 'tutar' ? <CheckIcon /> : <CopyIcon />}
                        </button>
                      ) : null}
                    </>
                  )
                })()}
              </div>
              <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
                {(() => {
                  const display = currentAltinSatis != null ? Number(currentAltinSatis).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' }) : '-'
                  return (
                    <>
                      <div><b>Has Altın Fiyatı:</b> {display}</div>
                      {currentAltinSatis != null ? (
                        <button aria-label="Kopyala" title={copied === 'has' ? 'Kopyalandı' : 'Kopyala'} onClick={() => copy('has', display)} style={{ background: 'none', border: 'none', padding: 4, cursor: 'pointer' }}>
                          {copied === 'has' ? <CheckIcon /> : <CopyIcon />}
                        </button>
                      ) : null}
                    </>
                  )
                })()}
              </div>
            </div>
            {(() => {
              const r2 = (n: number) => Math.round(n * 100) / 100
              const has = Number(currentAltinSatis || 0)
              const ay22 = (ayar === 22)
              const rawSaf = has * (ay22 ? 0.916 : 0.995)
              const u = parseFloat(amount.replace(',', '.')) || 0
              const rawYeni = u * (ay22 ? 0.99 : 0.998)
              const saf = r2(rawSaf)
              const yeni = r2(rawYeni)
              const gram = saf ? r2(yeni / saf) : 0
              const altinHizmet = r2(gram * saf)
              const iscilikKdvli = r2(r2(u) - altinHizmet)
              const isc = r2(iscilikKdvli / 1.20)
              return (
                <div style={{ marginTop: 12, display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
                  {(() => {
                    const display = saf ? saf.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' }) : '-'
                    return (
                      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
                        <div><b>Saf Altın Değeri:</b> {display}</div>
                        {saf ? (
                          <button aria-label="Kopyala" title={copied === 'saf' ? 'Kopyalandı' : 'Kopyala'} onClick={() => copy('saf', display)} style={{ background: 'none', border: 'none', padding: 4, cursor: 'pointer' }}>
                            {copied === 'saf' ? <CheckIcon /> : <CopyIcon />}
                          </button>
                        ) : null}
                      </div>
                    )
                  })()}
                  {(() => {
                    const display = yeni ? yeni.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' }) : '-'
                    return (
                      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
                        <div><b>Yeni Ürün Fiyatı:</b> {display}</div>
                        {yeni ? (
                          <button aria-label="Kopyala" title={copied === 'yeni' ? 'Kopyalandı' : 'Kopyala'} onClick={() => copy('yeni', display)} style={{ background: 'none', border: 'none', padding: 4, cursor: 'pointer' }}>
                            {copied === 'yeni' ? <CheckIcon /> : <CopyIcon />}
                          </button>
                        ) : null}
                      </div>
                    )
                  })()}
                  {(() => {
                    const display = gram ? gram.toLocaleString('tr-TR') : '-'
                    return (
                      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
                        <div><b>Gram Değeri:</b> {display}</div>
                        {gram ? (
                          <button aria-label="Kopyala" title={copied === 'gram' ? 'Kopyalandı' : 'Kopyala'} onClick={() => copy('gram', String(display))} style={{ background: 'none', border: 'none', padding: 4, cursor: 'pointer' }}>
                            {copied === 'gram' ? <CheckIcon /> : <CopyIcon />}
                          </button>
                        ) : null}
                      </div>
                    )
                  })()}
                  {(() => {
                    const display = isc ? isc.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' }) : '-'
                    return (
                      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
                        <div><b>İşçilik (KDV&apos;siz):</b> {display}</div>
                        {isc ? (
                          <button aria-label="Kopyala" title={copied === 'iscilik' ? 'Kopyalandı' : 'Kopyala'} onClick={() => copy('iscilik', display)} style={{ background: 'none', border: 'none', padding: 4, cursor: 'pointer' }}>
                            {copied === 'iscilik' ? <CheckIcon /> : <CopyIcon />}
                          </button>
                        ) : null}
                      </div>
                    )
                  })()}
                </div>
              )
            })()}
            <div className="actions" style={{ marginTop: 12, display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
              <button className="secondary" onClick={async () => { if (draftId) { try { await fetch(`${apiBase}/${txType === 'invoice' ? 'api/invoices' : 'api/expenses'}/${draftId}`, { method: 'DELETE', headers: { 'Content-Type': 'application/json', ...authHeaders() } }) } catch {} } setPreviewOpen(false); setPredictedSira(null); setDraftId(null) }}>İptal</button>
              <button className="primary" onClick={onSave} disabled={loading}>{loading ? 'Kaydediliyor…' : 'Onayla'}</button>
            </div>
          </div>
        </div>
      )}
      <SuccessToast message={success} />
      <ErrorToast message={error} />
    </main>
  )
}

export default function InvoiceNewPage() {
  return (
    <Suspense fallback={<div />}> 
      <InvoiceNewInner />
    </Suspense>
  )
}
