"use client"
import { useEffect, useMemo, useState } from 'react'
import { authHeaders } from '../../../lib/api'
import BackButton from '../../../components/BackButton'

type GoldItem = {
  code: string
  alis: number
  satis: number
  tarih: string
  alisDir?: string
  satisDir?: string
}

type CalcSettings = {
  defaultKariHesapla: boolean
  karMargin: number
  decimalPrecision: number
  karMilyemFormulaType: string
  showPercentage: boolean
  includeTax: boolean
  taxRate: number
}

type Payload = {
  metaTarih: string
  items: GoldItem[]
  precision: number
}

export default function GoldPricesPage() {
  const apiBase = useMemo(() => process.env.NEXT_PUBLIC_API_URL || '', [])
  const [payload, setPayload] = useState<Payload | null>(null)
  const [error, setError] = useState('')

  async function load() {
    setError('')
    try {
      // Load calc settings from backend (auth required)
      const sRes = await fetch(apiBase + '/api/settings/calc', { headers: { ...authHeaders() }, cache: 'no-store' })
      if (!sRes.ok) throw new Error('Ayarlar alinamadi')
      const calc = (await sRes.json()) as CalcSettings

      // Load feed cached by the backend
      const feedUrl = `${apiBase.replace(/\/$/, '')}/api/pricing/feed`
      const fRes = await fetch(feedUrl, { cache: 'no-store' })
      if (!fRes.ok) throw new Error('Fiyat verisine ulasilamadi')
      const j = await fRes.json()

      const metaTarih: string = String(j?.meta?.tarih ?? '')
      const data: Record<string, any> = j?.data || {}
      const items: GoldItem[] = []
      for (const [key, val] of Object.entries(data)) {
        if (val && typeof val === 'object') {
          const rawAlis = num(val['alis'])
          let rawSatis = num(val['satis'])

          // Apply calc settings similar to Flutter
          if (calc.defaultKariHesapla) {
            const margin = 1 + (toNum(calc.karMargin) / 100)
            rawSatis *= margin
          }
          if (calc.includeTax) {
            rawSatis *= 1 + (toNum(calc.taxRate) / 100)
          }

          items.push({
            code: key,
            alis: rawAlis,
            satis: rawSatis,
            tarih: String(val['tarih'] ?? metaTarih ?? ''),
            alisDir: String(val?.dir?.alis_dir ?? ''),
            satisDir: String(val?.dir?.satis_dir ?? ''),
          })
        }
      }

      setPayload({ metaTarih, items, precision: clampInt(calc.decimalPrecision, 0, 6) })
    } catch (e: any) {
      setError(e?.message || 'Bilinmeyen hata')
    }
  }

  useEffect(() => {
    load()
    const t = setInterval(load, 15000)
    return () => clearInterval(t)
  }, [])

  const f2 = (v?: number) => formatNumber(v, payload?.precision ?? 2)
  const label = (code: string) => LABEL_MAP[code] || code
  const dirColor = (dir?: string) => dir === 'up' ? '#1c8f4c' : dir === 'down' ? '#b3261e' : undefined

  const find = (code: string) => payload?.items.find(x => x.code.toUpperCase() === code.toUpperCase())

  const topList = TOP_CODES.map(c => find(c) || { code: c, alis: NaN, satis: NaN, tarih: payload?.metaTarih || '' })
  const ziynetList = ZIYNET_CODES.map(c => find(c) || { code: c, alis: NaN, satis: NaN, tarih: payload?.metaTarih || '' })
  const marqueeItems = ['USDTRY', 'EURTRY', 'GBPTRY', 'ONS'].map(c => find(c)).filter(Boolean) as GoldItem[]
  const marqueeText = marqueeItems.map(e => `${label(e.code)}: ${f2(e.satis)}`).join('   •   ')

  return (
    <main>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12 }}>
        <h1>Altın Fiyatları</h1>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <button className="secondary" onClick={load}>Yenile</button>
          <BackButton />
        </div>
      </div>

      {error && (
        <div className="card" style={{ background: '#fdecea', color: '#b3261e', fontWeight: 600 }}>{error}</div>
      )}

      <div style={{ marginTop: 8, opacity: 0.7 }}>Son Güncelleme: {payload?.metaTarih || '-'}</div>

      <div style={{ height: 8 }} />

      {/* Top grid */}
      <div className="card">
        <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: 12 }}>
          {topList.map((it) => (
            <div key={it.code} style={cardStyle}>
              <div style={{ fontWeight: 600 }}>{label(it.code)}</div>
              <div style={{ height: 8 }} />
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div>
                  <div style={{ opacity: 0.75 }}>Alış</div>
                  <div style={{ fontWeight: 700 }}>{f2(it.alis)}</div>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                  <span style={{ opacity: 0.75 }}>Satış</span>
                  <span style={{ fontWeight: 700 }}>{f2(it.satis)}</span>
                  {it.satisDir ? <span style={{ color: dirColor(it.satisDir) }}>{it.satisDir === 'up' ? '▲' : '▼'}</span> : null}
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      <div style={{ height: 16 }} />

      {/* Ziynet türleri */}
      <div className="card">
        <div style={{ fontSize: 16, fontWeight: 700, marginBottom: 8 }}>Ziynet Türleri</div>
        <div style={{ display: 'grid', gap: 8 }}>
          {ziynetList.map(it => (
            <div key={it.code} style={cardStyle}>
              <div style={{ fontWeight: 600 }}>{label(it.code)}</div>
              <div style={{ height: 6 }} />
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div>
                  <div style={{ opacity: 0.75 }}>Alış</div>
                  <div style={{ fontWeight: 700 }}>{f2(it.alis)}</div>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                  <span style={{ opacity: 0.75 }}>Satış</span>
                  <span style={{ fontWeight: 700 }}>{f2(it.satis)}</span>
                  {it.satisDir ? <span style={{ color: dirColor(it.satisDir) }}>{it.satisDir === 'up' ? '▲' : '▼'}</span> : null}
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      <div style={{ height: 16 }} />

      {/* Döviz kurları - marquee */}
      <div className="card">
        <div style={{ fontSize: 16, fontWeight: 700, marginBottom: 8 }}>Döviz Kurları</div>
        <Marquee text={marqueeText} />
      </div>

      <style jsx>{`
        @media (min-width: 520px) {
          .card > div:first-child { grid-template-columns: 1fr 1fr; }
        }
      `}</style>
    </main>
  )
}

const TOP_CODES = [
  'ALTIN', 'AYAR22', 'AYAR14', 'KULCEALTIN', 'ONS', 'USDTRY', 'EURTRY'
]
const ZIYNET_CODES = [
  'CEYREK_YENI', 'YARIM_YENI', 'TEK_YENI', 'ATA_YENI', 'ATA5_YENI'
]
const LABEL_MAP: Record<string, string> = {
  'ALTIN': 'Altın',
  'AYAR22': '22 Ayar',
  'AYAR14': '14 Ayar',
  'KULCEALTIN': 'Külçe',
  'ONS': 'ONS',
  'USDTRY': 'USD/TRY',
  'EURTRY': 'EUR/TRY',
  'GBPTRY': 'GBP/TRY',
  'CEYREK_YENI': 'Çeyrek (Yeni)',
  'YARIM_YENI': 'Yarım (Yeni)',
  'TEK_YENI': 'Tam (Yeni)',
  'ATA_YENI': 'Ata (Yeni)',
  'ATA5_YENI': '5xAta (Yeni)'
}

const cardStyle: React.CSSProperties = {
  background: '#fff',
  borderRadius: 12,
  padding: 12,
  boxShadow: '0 1px 4px rgba(0,0,0,0.08)'
}

function clampInt(v: number, min: number, max: number) { return Math.max(min, Math.min(max, Math.round(v))) }
function toNum(v: any): number { const n = Number(v); return Number.isFinite(n) ? n : 0 }
function num(v: any): number { const n = parseFloat(String(v).replace(',', '.')); return Number.isFinite(n) ? n : 0 }
function formatNumber(v?: number, fractionDigits = 2) {
  if (v == null || Number.isNaN(v)) return '-'
  try {
    return new Intl.NumberFormat('tr-TR', { minimumFractionDigits: fractionDigits, maximumFractionDigits: fractionDigits }).format(v)
  } catch { return String(v) }
}

function Marquee({ text }: { text: string }) {
  const [width, setWidth] = useState(0)
  const [textWidth, setTextWidth] = useState(0)

  useEffect(() => {
    const onResize = () => setWidth(window.innerWidth)
    onResize()
    window.addEventListener('resize', onResize)
    return () => window.removeEventListener('resize', onResize)
  }, [])

  return (
    <div style={{ overflow: 'hidden', position: 'relative', height: 28 }}>
      <MarqueeInner text={text} width={width} onMeasure={setTextWidth} textWidth={textWidth} />
    </div>
  )
}

function MarqueeInner({ text, width, onMeasure, textWidth }: { text: string, width: number, onMeasure: (w: number) => void, textWidth: number }) {
  const [t, setT] = useState(0)
  useEffect(() => {
    const id = setInterval(() => setT(x => (x + 1) % 1000000), 16)
    return () => clearInterval(id)
  }, [])
  const total = (textWidth || (text.length * 8)) + 40
  const dx = (width || 0) - ((t % (total)) )
  return (
    <div style={{ position: 'absolute', whiteSpace: 'nowrap', left: dx }}>
      <Measure onMeasure={onMeasure}>
        <span style={{ fontSize: 13, color: '#222' }}>{text}</span>
      </Measure>
      <span style={{ display: 'inline-block', width: 40 }} />
      <span style={{ fontSize: 13, color: '#222' }}>{text}</span>
    </div>
  )
}

function Measure({ children, onMeasure }: { children: React.ReactNode, onMeasure: (w: number) => void }) {
  const [ref, setRef] = useState<HTMLSpanElement | null>(null)
  useEffect(() => {
    if (ref) onMeasure(ref.getBoundingClientRect().width)
  }, [ref, onMeasure])
  return <span ref={setRef as any}>{children}</span>
}
