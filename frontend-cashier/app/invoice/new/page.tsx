"use client"
import { useEffect, useMemo, useState } from 'react'
import TouchField from '../../../components/ui/TouchField'
import BigKeypad from '../../../components/ui/BigKeypad'
import SuccessToast from '../../../components/ui/SuccessToast'
import ErrorToast from '../../../components/ui/ErrorToast'
import { useOfflineQueue } from '../../../hooks/useOfflineQueue'
import BackButton from '../../../components/BackButton'

function todayStr() {
  const d = new Date()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${d.getFullYear()}-${m}-${day}`
}

export default function InvoiceNewPage() {
  const apiBase = useMemo(() => process.env.NEXT_PUBLIC_API_URL || '', [])
  const { sendOrQueue } = useOfflineQueue(apiBase)

  const [date, setDate] = useState<string>(todayStr())
  const [firstName, setFirstName] = useState<string>('')
  const [lastName, setLastName] = useState<string>('')
  const [birthYear, setBirthYear] = useState<string>('')
  const [tckn, setTckn] = useState<string>('')
  const [amount, setAmount] = useState<string>('')
  const [payment, setPayment] = useState<'havale' | 'krediKartı' | ''>('')
  const [loading, setLoading] = useState(false)
  const [success, setSuccess] = useState('')
  const [error, setError] = useState('')
  const [activeKeypad, setActiveKeypad] = useState<'amount' | null>(null)
  const [tcknError, setTcknError] = useState<string>('')
  const [birthYearError, setBirthYearError] = useState<string>('')

  // live TCKN validation (algorithmic)
  function validateTCKN(id: string): boolean {
    if (!/^\d{11}$/.test(id)) return false
    if (id[0] === '0') return false
    const digits = id.split('').map((d) => parseInt(d, 10))
    const d1 = digits[0], d2 = digits[1], d3 = digits[2], d4 = digits[3], d5 = digits[4], d6 = digits[5], d7 = digits[6], d8 = digits[7], d9 = digits[8], d10 = digits[9], d11 = digits[10]
    const calc10 = (((d1 + d3 + d5 + d7 + d9) * 7) - (d2 + d4 + d6 + d8)) % 10
    const calc11 = (digits.slice(0, 10).reduce((a, b) => a + b, 0)) % 10
    return d10 === calc10 && d11 === calc11
  }

  useEffect(() => {
    if (!tckn) { setTcknError(''); return }
    setTcknError(validateTCKN(tckn) ? '' : 'TCKN geçersiz')
  }, [tckn])

  function validateBirthYear(y: string): boolean {
    if (!/^\d{4}$/.test(y)) return false
    const year = parseInt(y, 10)
    const now = new Date().getFullYear()
    return year >= 1900 && year <= now
  }

  useEffect(() => {
    if (!birthYear) { setBirthYearError(''); return }
    setBirthYearError(validateBirthYear(birthYear) ? '' : 'Doğum yılı geçersiz')
  }, [birthYear])

  const handleKeypadTo = (setter: React.Dispatch<React.SetStateAction<string>>) => ({ onKey: (k: string) => setter((v) => v + k), onBackspace: () => setter((v) => v.slice(0, -1)), onClear: () => setter('') })

  function sanitizeAmountInput(v: string): string {
    // Keep digits and at most one separator, normalize to comma for display
    const cleaned = v.replace(/[^0-9.,]/g, '')
    const parts = cleaned.replace(/\./g, ',').split(',')
    if (parts.length === 1) return parts[0]
    const intPart = parts[0]
    const fracPart = parts.slice(1).join('').slice(0, 2)
    return intPart + (fracPart ? (',' + fracPart) : '')
  }

  function formatAmountDisplay(v: string): string {
    if (!v) return ''
    const normalized = sanitizeAmountInput(v)
    const [intPart, fracPart] = normalized.split(',')
    const withSep = intPart.replace(/\B(?=(\d{3})+(?!\d))/g, '.')
    return fracPart != null && fracPart !== '' ? `${withSep},${fracPart}` : withSep
  }

  async function onSave() {
    setError('')
    setSuccess('')
    if (!date || !firstName || !lastName || !birthYear || !tckn || !amount || !payment) {
      setError('Lütfen tüm alanları doldurun')
      return
    }
    if (!validateTCKN(tckn)) {
      setError('TCKN geçersiz')
      return
    }
    if (!validateBirthYear(birthYear)) {
      setError('Doğum yılı geçersiz')
      return
    }
    const amountNum = parseFloat(amount.replace(',', '.'))
    if (Number.isNaN(amountNum)) {
      setError('Tutar geçersiz')
      return
    }
    setLoading(true)

    // Optional official verification
    const verifyRequired = (process.env.NEXT_PUBLIC_ID_VERIFY_REQUIRED || 'false') === 'true'
    const verifyPath = process.env.NEXT_PUBLIC_ID_VERIFY_URL || '/api/identity/verify'
    try {
      if (verifyPath && (process.env.NEXT_PUBLIC_ID_VERIFY_ENABLED || 'false') === 'true') {
        const res = await fetch(apiBase + verifyPath, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ tckn, firstName, lastName, birthYear: parseInt(birthYear, 10) })
        })
        if (!res.ok) {
          if (verifyRequired) {
            setLoading(false)
            setError('Resmi kimlik doğrulaması başarısız')
            return
          }
        } else {
          // optionally check body
          try {
            const data = await res.json()
            if (verifyRequired && !data?.verified) {
              setLoading(false)
              setError('Resmi kimlik doğrulaması olumsuz')
              return
            }
          } catch {
            // ignore parsing; rely on status when not required
          }
        }
      }
    } catch {
      if (verifyRequired) {
        setLoading(false)
        setError('Resmi doğrulama için bağlantı gerekli')
        return
      }
    }
    const payload: any = {
      // Backend DTO alanları
      tarih: date,
      siraNo: 0, // API otomatik atayacak
      musteriAdSoyad: `${firstName} ${lastName}`.trim(),
      tckn: tckn,
      tutar: amountNum,
      odemeSekli: payment === 'havale' ? 0 : 1,
      // Ek alanlar (API dikkate almaz ama resmi doğrulama için saklı tutuyoruz)
      fullName: `${firstName} ${lastName}`.trim(),
      firstName,
      lastName,
      birthYear: parseInt(birthYear, 10)
    }
    const res = await sendOrQueue('/api/invoices', payload)
    setLoading(false)
    if ((res as any).ok) {
      setSuccess('Kayıt eklendi ✅')
      // reset minimal
      setFirstName(''); setLastName(''); setBirthYear(''); setTckn(''); setAmount(''); setPayment('')
    } else if ((res as any).queued) {
      setSuccess('Çevrimdışı kaydedildi, sonra gönderilecek ✅')
      setFirstName(''); setLastName(''); setBirthYear(''); setTckn(''); setAmount(''); setPayment('')
    } else {
      setError('Kayıt eklenemedi')
    }
  }

  return (
    <main>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12 }}>
        <h1>Fatura Ekle</h1>
        <BackButton />
      </div>
      <div className="card" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        <TouchField label="Tarih" type="date" value={date} onChange={setDate} />
        <div className="row">
          <div style={{ flex: 1 }}>
            <TouchField label="Ad" value={firstName} onChange={setFirstName} onFocus={() => setActiveKeypad(null)} />
          </div>
          <div style={{ flex: 1 }}>
            <TouchField label="Soyad" value={lastName} onChange={setLastName} onFocus={() => setActiveKeypad(null)} />
          </div>
        </div>
        <TouchField label="Doğum Yılı" value={birthYear} onChange={(v) => setBirthYear(v.replace(/[^0-9]/g, '').slice(0,4))} inputMode="numeric" pattern="\d*" maxLength={4} onFocus={() => setActiveKeypad(null)} error={birthYearError} />
        <TouchField label="TCKN" value={tckn} onChange={(v) => setTckn(v.replace(/[^0-9]/g, '').slice(0, 11))} inputMode="numeric" pattern="\d*" maxLength={11} onFocus={() => setActiveKeypad(null)} error={tcknError} />
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          <TouchField label="Tutar" value={formatAmountDisplay(amount)} onChange={(v) => setAmount(sanitizeAmountInput(v))} inputMode="decimal" onFocus={() => setActiveKeypad('amount')} />
          {activeKeypad === 'amount' && (
            <BigKeypad {...handleKeypadTo(setAmount)} />
          )}
        </div>

        <div style={{ display: 'flex', gap: 12 }}>
          <button type="button" className={payment === 'havale' ? 'primary' : 'secondary'} onClick={() => setPayment('havale')} style={{ flex: 1 }}>Havale</button>
          <button type="button" className={payment === 'krediKartı' ? 'primary' : 'secondary'} onClick={() => setPayment('krediKartı')} style={{ flex: 1 }}>Kredi Kartı</button>
        </div>

        <div className="actions">
          <button className="primary" onClick={onSave} disabled={loading}>
            {loading ? 'Kaydediliyor…' : 'Kaydet'}
          </button>
        </div>
      </div>
      <SuccessToast message={success} />
      <ErrorToast message={error} />
    </main>
  )
}
