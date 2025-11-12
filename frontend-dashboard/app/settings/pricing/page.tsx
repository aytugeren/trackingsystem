"use client"
import { useEffect, useState } from 'react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'

type PriceSettings = { marginBuy: number; marginSell: number }

function authHeaders(): HeadersInit {
  try {
    const token = localStorage.getItem('ktp_token') || ''
    return token ? { Authorization: `Bearer ${token}` } : {}
  } catch { return {} }
}

async function getPriceSetting(code: string): Promise<PriceSettings> {
  const base = process.env.NEXT_PUBLIC_API_BASE || ''
  const res = await fetch(`${base}/api/pricing/settings/${code}`, { cache: 'no-store', headers: authHeaders() })
  if (!res.ok) throw new Error('Fiyat ayarları alınamadı')
  const j = await res.json()
  return { marginBuy: Number(j.marginBuy ?? 0), marginSell: Number(j.marginSell ?? 0) }
}

async function savePriceSetting(code: string, s: PriceSettings): Promise<void> {
  const base = process.env.NEXT_PUBLIC_API_BASE || ''
  const body = { code, marginBuy: Number(s.marginBuy ?? 0), marginSell: Number(s.marginSell ?? 0) }
  const res = await fetch(`${base}/api/pricing/settings/${code}`, { method: 'PUT', headers: { 'Content-Type': 'application/json', ...authHeaders() }, body: JSON.stringify(body) })
  if (!res.ok) throw new Error('Fiyat ayarları kaydedilemedi')
}

export default function PricingSettingsPage() {
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [ps22, setPs22] = useState<PriceSettings>({ marginBuy: 0, marginSell: 0 })
  const [ps24, setPs24] = useState<PriceSettings>({ marginBuy: 0, marginSell: 0 })
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    ;(async () => {
      try {
        setError('')
        setLoading(true)
        const [p22, p24] = await Promise.all([
          getPriceSetting('ALTIN_22').catch(() => ({ marginBuy: 0, marginSell: 0 })),
          getPriceSetting('ALTIN_24').catch(() => ({ marginBuy: 0, marginSell: 0 })),
        ])
        setPs22(p22)
        setPs24(p24)
      } catch { setError('Yüklenemedi') } finally { setLoading(false) }
    })()
  }, [])

  return (
    <Card>
      <CardHeader>
        <CardTitle>Fiyat Ayarları</CardTitle>
      </CardHeader>
      <CardContent>
        {error && <p className="text-red-600 mb-2">{error}</p>}
        {loading ? <p>Yükleniyor…</p> : (
          <div className="space-y-6">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="space-y-3">
                <div className="text-sm text-muted-foreground">22 Ayar</div>
                <div className="flex items-center gap-2">
                  <label className="w-40">Alış Marj (TL)</label>
                  <input type="number" step="0.01" value={ps22.marginBuy}
                         onChange={e => setPs22({ ...ps22, marginBuy: parseFloat(e.target.value || '0') })}
                         className="border rounded px-3 py-2 w-32" />
                </div>
                <div className="flex items-center gap-2">
                  <label className="w-40">Satış Marj (TL)</label>
                  <input type="number" step="0.01" value={ps22.marginSell}
                         onChange={e => setPs22({ ...ps22, marginSell: parseFloat(e.target.value || '0') })}
                         className="border rounded px-3 py-2 w-32" />
                </div>
              </div>
              <div className="space-y-3">
                <div className="text-sm text-muted-foreground">24 Ayar</div>
                <div className="flex items-center gap-2">
                  <label className="w-40">Alış Marj (TL)</label>
                  <input type="number" step="0.01" value={ps24.marginBuy}
                         onChange={e => setPs24({ ...ps24, marginBuy: parseFloat(e.target.value || '0') })}
                         className="border rounded px-3 py-2 w-32" />
                </div>
                <div className="flex items-center gap-2">
                  <label className="w-40">Satış Marj (TL)</label>
                  <input type="number" step="0.01" value={ps24.marginSell}
                         onChange={e => setPs24({ ...ps24, marginSell: parseFloat(e.target.value || '0') })}
                         className="border rounded px-3 py-2 w-32" />
                </div>
              </div>
            </div>
            <div className="mt-2">
              <Button disabled={saving} onClick={async () => { setSaving(true); try { await Promise.all([
                savePriceSetting('ALTIN_22', ps22),
                savePriceSetting('ALTIN_24', ps24),
              ]); if (typeof window !== 'undefined') alert('Fiyat ayarları kaydedildi') } finally { setSaving(false) } }}>Kaydet</Button>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  )
}

