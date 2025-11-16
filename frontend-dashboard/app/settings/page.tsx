"use client"
import { useEffect, useState } from 'react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'

type CalcSettings = {
  defaultKariHesapla: boolean
  karMargin: number
  decimalPrecision: number
  karMilyemFormulaType: 'basic' | 'withMargin' | 'custom'
  showPercentage: boolean
  includeTax: boolean
  taxRate: number
}

type KaratRange = { min: number; max: number; colorHex: string }
type KaratDiffSettings = { ranges: KaratRange[]; alertThreshold: number }

async function getMilyem(): Promise<number> {
  const base = process.env.NEXT_PUBLIC_API_BASE || ''
  const res = await fetch(`${base}/api/settings/milyem`, { cache: 'no-store', headers: authHeaders() })
  if (!res.ok) throw new Error('Ayar alınamadı')
  const j = await res.json()
  return Number(j.value ?? 1000)
}

async function setMilyem(v: number): Promise<void> {
  const base = process.env.NEXT_PUBLIC_API_BASE || ''
  const res = await fetch(`${base}/api/settings/milyem`, { method: 'PUT', headers: { 'Content-Type': 'application/json', ...authHeaders() }, body: JSON.stringify({ value: v }) })
  if (!res.ok) throw new Error('Ayar kaydedilemedi')
}

async function getCalcSettings(): Promise<CalcSettings> {
  const base = process.env.NEXT_PUBLIC_API_BASE || ''
  const res = await fetch(`${base}/api/settings/calc`, { cache: 'no-store', headers: authHeaders() })
  if (!res.ok) throw new Error('Ayarlar alınamadı')
  const j = await res.json()
  return {
    defaultKariHesapla: !!j.defaultKariHesapla,
    karMargin: Number(j.karMargin ?? 0),
    decimalPrecision: Number(j.decimalPrecision ?? 2),
    karMilyemFormulaType: (j.karMilyemFormulaType ?? 'basic'),
    showPercentage: !!j.showPercentage,
    includeTax: !!j.includeTax,
    taxRate: Number(j.taxRate ?? 0)
  }
}

async function saveCalcSettings(s: CalcSettings): Promise<void> {
  const base = process.env.NEXT_PUBLIC_API_BASE || ''
  const res = await fetch(`${base}/api/settings/calc`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', ...authHeaders() },
    body: JSON.stringify(s)
  })
  if (!res.ok) throw new Error('Ayarlar kaydedilemedi')
}

async function getKaratSettings(): Promise<KaratDiffSettings> {
  const base = process.env.NEXT_PUBLIC_API_BASE || ''
  const res = await fetch(`${base}/api/settings/karat`, { cache: 'no-store', headers: authHeaders() })
  if (!res.ok) throw new Error('Karat ayarları alınamadı')
  const j = await res.json()
  return { ranges: (j.ranges || []), alertThreshold: Number(j.alertThreshold ?? 1000) }
}

async function saveKaratSettings(s: KaratDiffSettings): Promise<void> {
  const base = process.env.NEXT_PUBLIC_API_BASE || ''
  const res = await fetch(`${base}/api/settings/karat`, { method: 'PUT', headers: { 'Content-Type': 'application/json', ...authHeaders() }, body: JSON.stringify(s) })
  if (!res.ok) throw new Error('Karat ayarları kaydedilemedi')
}

function authHeaders(): HeadersInit {
  try {
    const token = localStorage.getItem('ktp_token') || ''
    return token ? { Authorization: `Bearer ${token}` } : {}
  } catch { return {} }
}

export default function SettingsPage() {
  const [perms, setPerms] = useState<{ canManageSettings?: boolean; canManageKarat?: boolean } | null>(null)
  const [milyem, setMilyemState] = useState<number>(1000)
  const [calc, setCalc] = useState<CalcSettings>({
    defaultKariHesapla: true,
    karMargin: 0,
    decimalPrecision: 2,
    karMilyemFormulaType: 'basic',
    showPercentage: true,
    includeTax: false,
    taxRate: 0
  })
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [karat, setKarat] = useState<KaratDiffSettings>({ ranges: [
    { min: 100, max: 300, colorHex: '#FFF9C4' },
    { min: 300, max: 500, colorHex: '#FFCC80' },
    { min: 500, max: 700, colorHex: '#EF9A9A' },
    { min: 700, max: 1000, colorHex: '#D32F2F' },
  ], alertThreshold: 1000 })

  useEffect(() => {
    ;(async () => { try { const base = process.env.NEXT_PUBLIC_API_BASE || ''; const token = typeof window !== 'undefined' ? (localStorage.getItem('ktp_token') || '') : ''; const res = await fetch(`${base}/api/me/permissions`, { cache: 'no-store', headers: token ? { Authorization: `Bearer ${token}` } : {} }); if (res.ok) setPerms(await res.json()) } catch {} })()
    ;(async () => {
      try {
        setError('')
        setLoading(true)
        const v = await getMilyem()
        setMilyemState(v)
        const c = await getCalcSettings()
        setCalc(c)
        const k = await getKaratSettings()
        setKarat(k)
      } catch { setError('Yüklenemedi') } finally { setLoading(false) }
    })()
  }, [])

  if (!perms?.canManageSettings) return <p className="text-sm text-muted-foreground">Bu sayfa için yetkiniz yok.</p>

  return (
    <Card>
      <CardHeader>
        <CardTitle>Sistem Ayarları</CardTitle>
      </CardHeader>
      <CardContent>
        {error && <p className="text-red-600 mb-2">{error}</p>}
        {loading ? <p>Yükleniyor…</p> : (
          <div className="space-y-6">
            <div className="flex items-end gap-3">
              <div className="flex flex-col">
                <label className="text-sm text-muted-foreground">Kâr Milyemi (‰) — örn: 25 → +%2.5 satış</label>
                <input type="number" value={milyem} onChange={e => setMilyemState(parseFloat(e.target.value || '0'))} className="border rounded px-3 py-2 w-40 text-slate-900" />
              </div>
              <Button onClick={async () => { await setMilyem(milyem); if (typeof window !== 'undefined') alert('Milyem kaydedildi') }}>Kaydet</Button>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="flex items-center gap-2">
                <input id="defaultKariHesapla" type="checkbox" checked={calc.defaultKariHesapla} onChange={e => setCalc({ ...calc, defaultKariHesapla: e.target.checked })} />
                <label htmlFor="defaultKariHesapla">Kâr hesaplaması aktif</label>
              </div>

              <div className="flex items-center gap-2">
                <label className="w-40">Ek Kâr Oranı (%)</label>
                <input type="number" step="0.01" value={calc.karMargin} onChange={e => setCalc({ ...calc, karMargin: parseFloat(e.target.value || '0') })} className="border rounded px-3 py-2 w-32 text-slate-900" />
              </div>

              <div className="flex items-center gap-2">
                <label className="w-40">Virgül Basamak</label>
                <input type="number" value={calc.decimalPrecision} onChange={e => setCalc({ ...calc, decimalPrecision: parseInt(e.target.value || '0') })} className="border rounded px-3 py-2 w-24 text-slate-900" />
              </div>

              <div className="flex items-center gap-2">
                <label className="w-40">Formül Tipi</label>
                <select value={calc.karMilyemFormulaType} onChange={e => setCalc({ ...calc, karMilyemFormulaType: e.target.value as CalcSettings['karMilyemFormulaType'] })} className="border rounded px-3 py-2 w-40 text-slate-900">
                  <option value="basic">basic</option>
                  <option value="withMargin">withMargin</option>
                  <option value="custom">custom</option>
                </select>
              </div>

              <div className="flex items-center gap-2">
                <input id="showPercentage" type="checkbox" checked={calc.showPercentage} onChange={e => setCalc({ ...calc, showPercentage: e.target.checked })} />
                <label htmlFor="showPercentage">Kâr oranı % işareti ile</label>
              </div>

              <div className="flex items-center gap-2">
                <input id="includeTax" type="checkbox" checked={calc.includeTax} onChange={e => setCalc({ ...calc, includeTax: e.target.checked })} />
                <label htmlFor="includeTax">KDV dahil</label>
              </div>

              <div className="flex items-center gap-2">
                <label className="w-40">KDV Oranı (%)</label>
                <input type="number" step="0.01" value={calc.taxRate} disabled={!calc.includeTax} onChange={e => setCalc({ ...calc, taxRate: parseFloat(e.target.value || '0') })} className="border rounded px-3 py-2 w-32 text-slate-900" />
              </div>
            </div>

            <div>
              <Button onClick={async () => { await saveCalcSettings(calc); if (typeof window !== 'undefined') alert('Ayarlar kaydedildi') }}>Ayarları Kaydet</Button>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  )
}
