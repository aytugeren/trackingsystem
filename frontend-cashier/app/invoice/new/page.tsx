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
  const [draftId, setDraftId] = useState<string | null>(null)

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

  async function openPreview() {
    setError('')
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
        altinAyar: ayar
      }
      if (txType === 'invoice') payload.odemeSekli = payment === 'havale' ? 0 : 1
      const url = txType === 'invoice' ? '/api/cashier/invoices/draft' : '/api/cashier/expenses/draft'
      const r = await fetch(apiBase + url, { method: 'POST', headers, body: JSON.stringify(payload) })
      if (!r.ok) { setError('Önizleme oluşturulamadı'); return }
      const j = await r.json()
      setDraftId(String(j?.id))
      setPredictedSira(Number(j?.siraNo || 0))
      setCurrentAltinSatis(j?.altinSatisFiyati != null ? Number(j.altinSatisFiyati) : null)
    } catch {
      setError('Önizleme oluşturulamadı');
      return
    }
    setPreviewOpen(true)
  }

  
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
      altinAyar: ayar
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
      setFullName(''); setBirthYear(''); setTckn(''); setAmount(''); setPayment(''); setAyar(null); setPreviewOpen(false); setPredictedSira(null); setCurrentAltinSatis(null); setDraftId(null)
    } else if ((res as any).queued) {
      setSuccess('Çevrimdışı kaydedildi, sonra gönderilecek ✅')
      setFullName(''); setBirthYear(''); setTckn(''); setAmount(''); setPayment(''); setAyar(null); setPreviewOpen(false); setPredictedSira(null); setCurrentAltinSatis(null); setDraftId(null)
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

      <div style={{ display: 'flex', gap: 12, marginBottom: 12 }}>
        <button type="button" className={txType === 'invoice' ? 'primary' : 'secondary'} onClick={() => setTxType('invoice')} style={{ flex: 1 }}>Fatura</button>
        <button type="button" className={txType === 'expense' ? 'primary' : 'secondary'} onClick={() => setTxType('expense')} style={{ flex: 1 }}>Gider</button>
      </div>

      <div className="card" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        <TouchField label="Tarih" type="date" value={date} onChange={setDate} />
        <TouchField label="Ad Soyad" value={fullName} onChange={setFullName} onFocus={() => setActiveKeypad(null)} />
        {/* Doğum yılı alanı kaldırıldı */}
        <TouchField label="TCKN" value={tckn} onChange={(v) => setTckn(v.replace(/[^0-9]/g, '').slice(0, 11))} inputMode="numeric" pattern="\d*" maxLength={11} onFocus={() => setActiveKeypad(null)} error={tcknError} />
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          <TouchField label="Tutar" value={formatAmountDisplay(amount)} onChange={(v) => setAmount(sanitizeAmountInput(v))} inputMode="decimal" onFocus={() => setActiveKeypad('amount')} />
          {activeKeypad === 'amount' && (
            <BigKeypad {...handleKeypadTo(setAmount)} />
          )}
        </div>

        <div style={{ display: 'flex', gap: 12 }}>
          <button type="button" className={ayar === 22 ? 'primary' : 'secondary'} onClick={() => setAyar(22)} style={{ flex: 1 }}>22 Ayar</button>
          <button type="button" className={ayar === 24 ? 'primary' : 'secondary'} onClick={() => setAyar(24)} style={{ flex: 1 }}>24 Ayar</button>
        </div>

        {txType === 'invoice' && (
          <div style={{ display: 'flex', gap: 12 }}>
            <button type="button" className={payment === 'havale' ? 'primary' : 'secondary'} onClick={() => setPayment('havale')} style={{ flex: 1 }}>Havale</button>
            <button type="button" className={payment === 'krediKarti' ? 'primary' : 'secondary'} onClick={() => setPayment('krediKarti')} style={{ flex: 1 }}>Kredi Kartı</button>
          </div>
        )}

        <div className="actions">
          <button className="primary" onClick={openPreview} disabled={loading}>
            {loading ? 'Hazırlanıyor…' : 'Önizle'}
          </button>
        </div>
      </div>
      {previewOpen && (
        <div className="modal-overlay" style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 50 }}>
          <div className="modal" style={{ background: '#fff', borderRadius: 8, padding: 16, width: '100%', maxWidth: 600 }}>
            <h2 style={{ marginTop: 0 }}>{txType === 'invoice' ? 'Fatura Bilgileri' : 'Gider Bilgileri'}</h2>
            <div style={{ fontSize: 14, color: '#555', marginBottom: 8 }}>Sıra No: <b>{predictedSira ?? '-'}</b></div>
            <div className="card" style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
              <div><b>Ad Soyad:</b> {fullName}</div>
              <div><b>TCKN:</b> {tckn}</div>
              <div><b>Tarih:</b> {date}</div>
              {txType === 'invoice' && <div><b>Ödeme:</b> {payment === 'havale' ? 'Havale' : 'Kredi Kartı'}</div>}
              <div><b>Ayar:</b> {ayar ? (ayar === 22 ? '22 Ayar' : '24 Ayar') : '-'}</div>
              <div><b>Tutar:</b> {amount ? Number(parseFloat(amount.replace(',', '.'))).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' }) : '-'}</div>
              <div><b>Has Altın Fiyatı:</b> {currentAltinSatis != null ? Number(currentAltinSatis).toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' }) : '-'}</div>
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
                  <div><b>Saf Altın Değeri:</b> {saf ? saf.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' }) : '-'}</div>
                  <div><b>Yeni Ürün Fiyatı:</b> {yeni ? yeni.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' }) : '-'}</div>
                  <div><b>Gram Değeri:</b> {gram ? gram.toLocaleString('tr-TR') : '-'}</div>
                  <div><b>İşçilik (KDV’siz):</b> {isc ? isc.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' }) : '-'}</div>
                </div>
              )
            })()}
            <div className="actions" style={{ marginTop: 12, display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
              <button className="secondary" onClick={async () => { if (draftId) { try { await fetch(`${apiBase}/${txType === 'invoice' ? 'api/invoices' : 'api/expenses'}/${draftId}`, { method: 'DELETE', headers: { 'Content-Type': 'application/json', ...authHeaders() } }) } catch {} } setPreviewOpen(false); setPredictedSira(null); setCurrentAltinSatis(null); setDraftId(null) }}>İptal</button>
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



















