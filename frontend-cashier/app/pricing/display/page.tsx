"use client"
import { useEffect, useMemo, useState } from 'react'
import SuccessToast from '../../../components/ui/SuccessToast'
import ErrorToast from '../../../components/ui/ErrorToast'
import BackButton from '../../../components/BackButton'

type Latest = {
  code: string
  alis: number
  satis: number
  finalAlis: number
  finalSatis: number
  sourceTime: string
}

export default function PricingDisplayPage() {
  const apiBase = useMemo(() => process.env.NEXT_PUBLIC_API_URL || '', [])
  const [data, setData] = useState<Latest | null>(null)
  const [loading, setLoading] = useState(false)
  const [success, setSuccess] = useState('')
  const [error, setError] = useState('')

  async function load() {
    setError('')
    try {
      const res = await fetch(apiBase + '/api/pricing/ALTIN/latest', { cache: 'no-store' })
      if (!res.ok) throw new Error('Fiyat bulunamadı')
      const j = await res.json()
      setData(j)
    } catch (e: any) {
      setError(e.message || 'Fiyat alınamadı')
    }
  }

  async function refreshFeed() {
    setLoading(true)
    setError('')
    setSuccess('')
    try {
      const res = await fetch(apiBase + '/api/pricing/refresh', { method: 'POST' })
      if (!res.ok) throw new Error('Güncelleme başarısız')
      setSuccess('Güncellendi ✓')
      await load()
    } catch (e: any) {
      setError(e.message || 'Güncelleme başarısız')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    load()
    const t = setInterval(load, 30000)
    return () => clearInterval(t)
  }, [])

  return (
    <main>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12 }}>
        <h1>ALTIN Fiyatı</h1>
        <BackButton />
      </div>
      <div className="card" style={{ textAlign: 'center' }}>
        <div style={{ display: 'flex', gap: 12, justifyContent: 'space-between' }}>
          <div style={{ flex: 1, background: '#eef7f2', borderRadius: 12, padding: 12 }}>
            <div style={{ opacity: 0.7 }}>Alış</div>
            <div style={{ fontSize: 36, fontWeight: 800 }}>{formatTL(data?.finalAlis)}</div>
            <div style={{ opacity: 0.6, fontSize: 14 }}>Kaynak: {formatTL(data?.alis)}</div>
          </div>
          <div style={{ flex: 1, background: '#f6eef2', borderRadius: 12, padding: 12 }}>
            <div style={{ opacity: 0.7 }}>Satış</div>
            <div style={{ fontSize: 36, fontWeight: 800 }}>{formatTL(data?.finalSatis)}</div>
            <div style={{ opacity: 0.6, fontSize: 14 }}>Kaynak: {formatTL(data?.satis)}</div>
          </div>
        </div>
        <div style={{ marginTop: 12, opacity: 0.7 }}>Gün: {data?.sourceTime ? new Date(data.sourceTime).toLocaleString('tr-TR') : '-'}</div>
        <div className="actions" style={{ justifyContent: 'center' }}>
          <button className="primary" disabled={loading} onClick={refreshFeed}>{loading ? 'Güncelleniyor…' : 'Güncelle'}</button>
          <button className="secondary" onClick={load}>Yenile</button>
        </div>
      </div>
      <SuccessToast message={success} />
      <ErrorToast message={error} />
    </main>
  )
}

function formatTL(v?: number) {
  if (v == null || Number.isNaN(v)) return '-'
  return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', maximumFractionDigits: 2 }).format(v)
}