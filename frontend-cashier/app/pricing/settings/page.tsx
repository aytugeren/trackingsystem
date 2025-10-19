"use client"
import { useEffect, useMemo, useState } from 'react'
import TouchField from '../../../components/ui/TouchField'
import SuccessToast from '../../../components/ui/SuccessToast'
import ErrorToast from '../../../components/ui/ErrorToast'
import BackButton from '../../../components/BackButton'

export default function PricingSettingsPage() {
  const apiBase = useMemo(() => process.env.NEXT_PUBLIC_API_URL || '', [])
  const [marginBuy, setMarginBuy] = useState<string>('0')
  const [marginSell, setMarginSell] = useState<string>('0')
  const [loading, setLoading] = useState(false)
  const [success, setSuccess] = useState('')
  const [error, setError] = useState('')

  async function load() {
    setError('')
    try {
      const res = await fetch(apiBase + '/api/pricing/settings/ALTIN', { cache: 'no-store' })
      if (!res.ok) throw new Error('Ayarlar alınamadı')
      const j = await res.json()
      setMarginBuy(String(j.marginBuy ?? 0))
      setMarginSell(String(j.marginSell ?? 0))
    } catch (e: any) {
      setError(e.message || 'Ayarlar alınamadı')
    }
  }

  async function save() {
    setLoading(true)
    setError('')
    setSuccess('')
    try {
      const body = {
        code: 'ALTIN',
        marginBuy: parseFloat(marginBuy.replace(',', '.')) || 0,
        marginSell: parseFloat(marginSell.replace(',', '.')) || 0,
      }
      const res = await fetch(apiBase + '/api/pricing/settings/ALTIN', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      })
      if (!res.ok) throw new Error('Kaydedilemedi')
      setSuccess('Kaydedildi ✓')
    } catch (e: any) {
      setError(e.message || 'Kaydedilemedi')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { load() }, [])

  return (
    <main>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12 }}>
        <h1>Fiyat Ayarları (ALTIN)</h1>
        <BackButton />
      </div>
      <div className="card" style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
        <TouchField label="Alış Marj (TL)" value={marginBuy} onChange={setMarginBuy} inputMode="decimal" />
        <TouchField label="Satış Marj (TL)" value={marginSell} onChange={setMarginSell} inputMode="decimal" />
        <div className="actions">
          <button className="primary" onClick={save} disabled={loading}>{loading ? 'Kaydediliyor…' : 'Kaydet'}</button>
        </div>
      </div>
      <SuccessToast message={success} />
      <ErrorToast message={error} />
    </main>
  )
}