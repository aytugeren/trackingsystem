"use client"
import { useEffect, useMemo, useState } from 'react'
import TouchField from '../../../components/ui/TouchField'
import BigKeypad from '../../../components/ui/BigKeypad'
import SuccessToast from '../../../components/ui/SuccessToast'
import ErrorToast from '../../../components/ui/ErrorToast'
import { useOfflineQueue } from '../../../hooks/useOfflineQueue'

function todayStr() {
  const d = new Date()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${d.getFullYear()}-${m}-${day}`
}

export default function ExpenseNewPage() {
  const apiBase = useMemo(() => process.env.NEXT_PUBLIC_API_URL || '', [])
  const { sendOrQueue } = useOfflineQueue(apiBase)

  const [date, setDate] = useState<string>(todayStr())
  const [firstName, setFirstName] = useState<string>('')
  const [lastName, setLastName] = useState<string>('')
  const [birthYear, setBirthYear] = useState<string>('')
  const [tckn, setTckn] = useState<string>('')
  const [amount, setAmount] = useState<string>('')
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

  async function onSave() {
    setError('')
    setSuccess('')
    if (!date || !firstName || !lastName || !birthYear || !tckn || !amount) {
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
    const payload: any = {
      tarih: date,
      siraNo: 0,
      musteriAdSoyad: `${firstName} ${lastName}`.trim(),
      tckn: tckn,
      tutar: amountNum,
      // Ek alanlar (API dikkate almaz)
      fullName: `${firstName} ${lastName}`.trim(),
      firstName,
      lastName,
      birthYear: parseInt(birthYear, 10)
    }
    const res = await sendOrQueue('/api/expenses', payload)
    setLoading(false)
    if ((res as any).ok) {
      setSuccess('Kayıt eklendi ✅')
      setFirstName(''); setLastName(''); setBirthYear(''); setTckn(''); setAmount('')
    } else if ((res as any).queued) {
      setSuccess('Çevrimdışı kaydedildi, sonra gönderilecek ✅')
      setFirstName(''); setLastName(''); setBirthYear(''); setTckn(''); setAmount('')
    } else {
      setError('Kayıt eklenemedi')
    }
  }

  return (
    <main>
      <h1>Gider Ekle</h1>
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
          <TouchField label="Tutar" value={amount} onChange={setAmount} inputMode="decimal" onFocus={() => setActiveKeypad('amount')} />
          {activeKeypad === 'amount' && (
            <BigKeypad {...handleKeypadTo(setAmount)} />
          )}
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
