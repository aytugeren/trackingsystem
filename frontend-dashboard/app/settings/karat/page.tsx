"use client"
import { useEffect, useState } from 'react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'

type KaratRange = { min: number; max: number; colorHex: string }
type KaratDiffSettings = { ranges: KaratRange[]; alertThreshold: number }

function authHeaders(): HeadersInit {
  try {
    const token = localStorage.getItem('ktp_token') || ''
    return token ? { Authorization: `Bearer ${token}` } : {}
  } catch { return {} }
}

async function getKaratSettings(): Promise<KaratDiffSettings> {
  const base = process.env.NEXT_PUBLIC_API_BASE || ''
  const res = await fetch(`${base}/api/settings/karat`, { cache: 'no-store', headers: authHeaders() })
  if (!res.ok) throw new Error('Ayar alinamadi')
  const j = await res.json()
  return { ranges: (j.ranges || []), alertThreshold: Number(j.alertThreshold ?? 1000) }
}

async function saveKaratSettings(s: KaratDiffSettings): Promise<void> {
  const base = process.env.NEXT_PUBLIC_API_BASE || ''
  const res = await fetch(`${base}/api/settings/karat`, { method: 'PUT', headers: { 'Content-Type': 'application/json', ...authHeaders() }, body: JSON.stringify(s) })
  if (!res.ok) throw new Error('Kayit basarisiz')
}

export default function KaratSettingsPage() {
  const [perms, setPerms] = useState<{ canManageKarat?: boolean } | null>(null)
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
        setError(''); setLoading(true)
        const k = await getKaratSettings()
        setKarat(k)
      } catch { setError('Yüklenemedi') } finally { setLoading(false) }
    })()
  }, [])

  if (!perms?.canManageKarat) return <p className="text-sm text-muted-foreground">Bu sayfa için yetkiniz yok.</p>

  return (
    <Card>
      <CardHeader>
        <CardTitle>Karat Fark Görselleştirme</CardTitle>
      </CardHeader>
      <CardContent>
        {error && <p className="text-red-600 mb-2">{error}</p>}
        {loading ? <p>Yükleniyor...</p> : (
          <div className="space-y-6">
            <div className="flex items-center gap-2">
              <label className="w-64">Uyarı Eşiği (gr)</label>
              <input type="number" value={karat.alertThreshold} onChange={e => setKarat({ ...karat, alertThreshold: parseFloat(e.target.value || '0') })} className="border rounded px-3 py-2 w-40 text-slate-900" />
            </div>
            <div className="space-y-2">
              <div className="text-sm text-muted-foreground">Aralıklar (min ≤ fark &lt; max) ve renk (#RRGGBB):</div>
              {karat.ranges.map((r, idx) => (
                <div key={idx} className="grid grid-cols-1 md:grid-cols-5 gap-2 items-center">
                  <div className="flex items-center gap-2">
                    <label className="w-20">Min</label>
                    <input type="number" value={r.min} onChange={e => {
                      const v = parseFloat(e.target.value || '0');
                      setKarat({ ...karat, ranges: karat.ranges.map((x, i) => i === idx ? { ...x, min: v } : x) })
                    }} className="border rounded px-3 py-2 w-28 text-slate-900" />
                  </div>
                  <div className="flex items-center gap-2">
                    <label className="w-20">Max</label>
                    <input type="number" value={r.max} onChange={e => {
                      const v = parseFloat(e.target.value || '0');
                      setKarat({ ...karat, ranges: karat.ranges.map((x, i) => i === idx ? { ...x, max: v } : x) })
                    }} className="border rounded px-3 py-2 w-28 text-slate-900" />
                  </div>
                  <div className="flex items-center gap-2 col-span-2">
                    <label className="w-20">Renk</label>
                    <input type="text" value={r.colorHex} onChange={e => setKarat({ ...karat, ranges: karat.ranges.map((x, i) => i === idx ? { ...x, colorHex: e.target.value } : x) })} className="border rounded px-3 py-2 w-40 text-slate-900" placeholder="#RRGGBB" />
                    <input type="color" value={/^#[0-9A-Fa-f]{6}$/.test(r.colorHex) ? r.colorHex : '#FFFFFF'} onChange={e => setKarat({ ...karat, ranges: karat.ranges.map((x, i) => i === idx ? { ...x, colorHex: e.target.value } : x) })} className="w-10 h-10 border rounded text-slate-900" />
                    <Button variant="outline" onClick={() => setKarat({ ...karat, ranges: karat.ranges.filter((_, i) => i !== idx) })}>Sil</Button>
                  </div>
                </div>
              ))}
              <div>
                <Button variant="outline" onClick={() => setKarat({ ...karat, ranges: [...karat.ranges, { min: 0, max: 0, colorHex: '#FFFFFF' }] })}>Aralık Ekle</Button>
              </div>
            </div>
            <div>
              <Button onClick={async () => { await saveKaratSettings(karat); try { window.dispatchEvent(new CustomEvent('ktp:karat-updated')) } catch {}; if (typeof window !== 'undefined') alert('Karat ayarları kaydedildi') }}>Kaydet</Button>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  )
}
