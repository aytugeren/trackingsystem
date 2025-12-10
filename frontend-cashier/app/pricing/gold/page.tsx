"use client"
import { useEffect, useMemo, useState } from 'react'
import BackButton from '../../../components/BackButton'
import { authHeaders } from '../../../lib/api'

type GoldInfo = {
  price: number
  updatedAt: string | null
  updatedBy?: string | null
}

export default function GoldPricePage() {
  const apiBase = useMemo(() => process.env.NEXT_PUBLIC_API_URL || '', [])
  const [info, setInfo] = useState<GoldInfo | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')
  const [editing, setEditing] = useState(false)
  const [input, setInput] = useState('')
  const [saving, setSaving] = useState(false)

  async function load() {
    setLoading(true)
    setError('')
    try {
      const res = await fetch(apiBase + '/api/pricing/gold', { cache: 'no-store', headers: { ...authHeaders() } })
      if (!res.ok) throw new Error('Has Altın bilgisi alınamadı')
      const j = await res.json()
      const priceVal = Number(j?.price ?? 0)
      setInfo({ price: priceVal, updatedAt: j?.updatedAt ?? null, updatedBy: j?.updatedBy ?? null })
      setInput(priceVal ? String(priceVal) : '')
    } catch (e: any) {
      setError(e?.message || 'Has Altın bilgisi alınamadı')
      setInfo(null)
    } finally {
      setLoading(false)
    }
  }

  async function save() {
    const parsed = parseFloat((input || '').replace(',', '.'))
    if (!parsed || Number.isNaN(parsed)) {
      setError('Geçerli bir has altın değeri girin')
      return
    }
    setSaving(true)
    setError('')
    try {
      const res = await fetch(apiBase + '/api/pricing/gold', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json', ...authHeaders() },
        body: JSON.stringify({ price: parsed })
      })
      if (!res.ok) throw new Error('Has Altın güncellenemedi')
      const j = await res.json()
      const priceVal = Number(j?.price ?? parsed)
      setInfo({ price: priceVal, updatedAt: j?.updatedAt ?? null, updatedBy: j?.updatedBy ?? null })
      setEditing(false)
    } catch (e: any) {
      setError(e?.message || 'Has Altın güncellenemedi')
    } finally {
      setSaving(false)
    }
  }

  useEffect(() => { load() }, [])

  const formattedPrice = info?.price != null
    ? new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', maximumFractionDigits: 3 }).format(info.price)
    : 'Has altın girilmedi'
  const formattedMeta = info?.updatedAt
    ? `Son: ${new Date(info.updatedAt).toLocaleString('tr-TR')}${info?.updatedBy ? ' - ' + info.updatedBy : ''}`
    : (info?.updatedBy ? `Guncelleyen: ${info.updatedBy}` : 'Henuz guncellenmedi')

  return (
    <main>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12 }}>
        <h1>Has Altın</h1>
        <BackButton />
      </div>

      {error && (
        <div className="card" style={{ background: '#fdecea', color: '#b3261e', fontWeight: 600 }}>{error}</div>
      )}

      <div className="card" style={{ display: 'flex', flexDirection: 'column', gap: 10, padding: 16, maxWidth: 420 }}>
        {loading ? (
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <div className="spinner" />
            <div>Yükleniyor...</div>
          </div>
        ) : editing ? (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <label style={{ fontWeight: 700 }}>Has Altın</label>
            <input
              type="number"
              inputMode="decimal"
              step="0.001"
              value={input}
              onChange={(e) => setInput(e.target.value)}
              style={{ padding: 10, fontSize: 16, borderRadius: 6, border: '1px solid #ccc' }}
              placeholder="Has altın fiyatı"
            />
            <div style={{ display: 'flex', gap: 8 }}>
              <button className="primary" onClick={save} disabled={saving}>{saving ? 'Kaydediliyor…' : 'Kaydet'}</button>
              <button className="secondary" onClick={() => setEditing(false)} disabled={saving}>Vazgeç</button>
            </div>
          </div>
        ) : (
          <button
            onClick={() => { setEditing(true); setInput(info?.price ? String(info.price) : '') }}
            style={{
              background: 'none',
              border: 'none',
              textAlign: 'left',
              padding: 0,
              cursor: 'pointer',
              display: 'flex',
              flexDirection: 'column',
              gap: 6
            }}
          >
            <div style={{ fontWeight: 700, color: '#111' }}>Has Altın</div>
            <div style={{ fontSize: 24, fontWeight: 800, color: '#111' }}>{formattedPrice}</div>
            <div style={{ fontSize: 11, color: '#666' }}>{formattedMeta}</div>
          </button>
        )}
      </div>
    </main>
  )
}
