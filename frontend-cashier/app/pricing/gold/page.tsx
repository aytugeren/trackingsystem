"use client"
import { useEffect, useMemo, useState } from 'react'
import BackButton from '../../../components/BackButton'
import { authHeaders } from '../../../lib/api'

type GoldInfo = {
  price: number
  updatedAt: string | null
  updatedBy?: string | null
}

type GoldFeedHeader = {
  usdAlis: number
  usdSatis: number
  eurAlis: number
  eurSatis: number
  eurUsd: number
  ons: number
  has: number
  gumusHas: number
}

type GoldFeedItem = {
  index: number
  label: string
  isUsed: boolean
  value: number | null
}

type GoldFeedLatest = {
  fetchedAt: string
  header: GoldFeedHeader
  items: GoldFeedItem[]
}

export default function GoldPricePage() {
  const apiBase = useMemo(() => process.env.NEXT_PUBLIC_API_URL || '', [])
  const [info, setInfo] = useState<GoldInfo | null>(null)
  const [feed, setFeed] = useState<GoldFeedLatest | null>(null)
  const [loading, setLoading] = useState(false)
  const [feedLoading, setFeedLoading] = useState(false)
  const [error, setError] = useState('')
  const [feedError, setFeedError] = useState('')
  const [editing, setEditing] = useState(false)
  const [input, setInput] = useState('')
  const [saving, setSaving] = useState(false)
  const [search, setSearch] = useState('')
  const [pendingSearch, setPendingSearch] = useState('')
  const [priceMode, setPriceMode] = useState<'alis' | 'satis'>('satis')

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

  async function loadFeed() {
    setFeedLoading(true)
    setFeedError('')
    try {
      const res = await fetch(apiBase + '/api/pricing/feed/latest', { cache: 'no-store', headers: { ...authHeaders() } })
      if (!res.ok) throw new Error('Altın fiyatları alınamadı')
      const j = await res.json()
      setFeed(j)
    } catch (e: any) {
      setFeedError(e?.message || 'Altın fiyatları alınamadı')
      setFeed(null)
    } finally {
      setFeedLoading(false)
    }
  }

  useEffect(() => { load() }, [])
  useEffect(() => { loadFeed() }, [])
  useEffect(() => {
    const t = setTimeout(() => { setSearch(pendingSearch) }, 200)
    return () => clearTimeout(t)
  }, [pendingSearch])

  const formattedPrice = info?.price != null
    ? new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', maximumFractionDigits: 3 }).format(info.price)
    : 'Has altın girilmedi'
  const formattedMeta = info?.updatedAt
    ? `Son: ${new Date(info.updatedAt).toLocaleString('tr-TR')}${info?.updatedBy ? ' - ' + info.updatedBy : ''}`
    : (info?.updatedBy ? `Guncelleyen: ${info.updatedBy}` : 'Henuz guncellenmedi')

  const feedItems = feed?.items?.filter((x) => x.isUsed) ?? []
  const normalizeText = (value: string) =>
    value
      .toLowerCase()
      .replace(/[ıİ]/g, 'i')
      .replace(/[şŞ]/g, 's')
      .replace(/[ğĞ]/g, 'g')
      .replace(/[üÜ]/g, 'u')
      .replace(/[öÖ]/g, 'o')
      .replace(/[çÇ]/g, 'c')

  const filteredFeedItems = useMemo(() => {
    const q = normalizeText(search.trim())
    return feedItems.filter((item) => {
      const labelRaw = item.label || ''
      const label = normalizeText(labelRaw)
      const isBuy = label.includes('alis')
      const isSell = label.includes('satis')
      if (priceMode === 'alis' && !isBuy) return false
      if (priceMode === 'satis' && !isSell) return false
      if (!q) return true
      return label.includes(q) || String(item.index).includes(q)
    })
  }, [feedItems, search, priceMode])
  const formatNumber = (value: number | null | undefined) =>
    value == null ? '-' : new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 3 }).format(value)

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

      <div className="card" style={{ marginTop: 16, display: 'flex', flexDirection: 'column', gap: 10, padding: 16 }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12 }}>
          <div style={{ fontWeight: 700 }}>Altın Fiyatları</div>
          <button className="secondary" onClick={loadFeed} disabled={feedLoading}>{feedLoading ? 'Yükleniyor…' : 'Yenile'}</button>
        </div>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          <div style={{ display: 'flex', gap: 6 }}>
            <button
              className={priceMode === 'alis' ? 'primary' : 'secondary'}
              onClick={() => setPriceMode('alis')}
            >
              Alış
            </button>
            <button
              className={priceMode === 'satis' ? 'primary' : 'secondary'}
              onClick={() => setPriceMode('satis')}
            >
              Satış
            </button>
          </div>
          <input
            type="text"
            value={pendingSearch}
            onChange={(e) => setPendingSearch(e.target.value)}
            placeholder="Ürün adı ile ara"
            style={{ padding: 8, fontSize: 14, borderRadius: 6, border: '1px solid #ccc', flex: '1 1 200px' }}
          />
          <button className="secondary" onClick={() => { setPendingSearch(''); setSearch('') }} disabled={!pendingSearch && !search}>Temizle</button>
        </div>

        {feedError && (
          <div style={{ background: '#fdecea', color: '#b3261e', fontWeight: 600, padding: 10, borderRadius: 6 }}>{feedError}</div>
        )}

        {feedLoading ? (
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <div className="spinner" />
            <div>Yükleniyor...</div>
          </div>
        ) : feed ? (
          <>
            <div style={{ fontSize: 12, color: '#666' }}>
              Son çekim: {new Date(feed.fetchedAt).toLocaleString('tr-TR')}
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 8 }}>
              <div>USD Alış</div><div style={{ fontWeight: 600 }}>{formatNumber(feed.header.usdAlis)}</div>
              <div>USD Satış</div><div style={{ fontWeight: 600 }}>{formatNumber(feed.header.usdSatis)}</div>
              <div>EURO Alış</div><div style={{ fontWeight: 600 }}>{formatNumber(feed.header.eurAlis)}</div>
              <div>EURO Satış</div><div style={{ fontWeight: 600 }}>{formatNumber(feed.header.eurSatis)}</div>
              <div>EURO/USD</div><div style={{ fontWeight: 600 }}>{formatNumber(feed.header.eurUsd)}</div>
              <div>ONS</div><div style={{ fontWeight: 600 }}>{formatNumber(feed.header.ons)}</div>
              <div>HAS</div><div style={{ fontWeight: 600 }}>{formatNumber(feed.header.has)}</div>
              <div>GümüşHAS</div><div style={{ fontWeight: 600 }}>{formatNumber(feed.header.gumusHas)}</div>
            </div>

            <div style={{ marginTop: 12, fontWeight: 700 }}>Sabit Liste</div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
              {filteredFeedItems.map((item) => (
                <div key={item.index} style={{ display: 'flex', justifyContent: 'space-between', gap: 12 }}>
                  <div>{item.index}. {item.label}</div>
                  <div style={{ fontWeight: 600 }}>{formatNumber(item.value)}</div>
                </div>
              ))}
              {filteredFeedItems.length === 0 && (
                <div style={{ color: '#666' }}>Sonuç bulunamadı.</div>
              )}
            </div>
          </>
        ) : (
          <div>Altın fiyatları alınamadı.</div>
        )}
      </div>
    </main>
  )
}
