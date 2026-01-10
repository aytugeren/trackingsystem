"use client"
import { useEffect, useMemo, useState } from 'react'
import { api, type DashboardSummary } from '@/lib/api'

export default function GlobalKaratAlertFixed() {
  const [summary, setSummary] = useState<DashboardSummary | null>(null)
  const [cfg, setCfg] = useState<{ alertThreshold: number } | null>(null)
  const [refreshTick, setRefreshTick] = useState(0)

  const loadTx = async () => {
    try {
      const data = await api.dashboardSummary({ mode: 'all' })
      setSummary(data)
    } catch {}
  }
  const loadCfg = async () => {
    try {
      const base = process.env.NEXT_PUBLIC_API_BASE || ''
      const token = typeof window !== 'undefined' ? (localStorage.getItem('ktp_token') || '') : ''
      const res = await fetch(`${base}/api/settings/karat`, { cache: 'no-store', headers: token ? { Authorization: `Bearer ${token}` } : {} })
      if (res.ok) {
        const j = await res.json()
        setCfg({ alertThreshold: Number(j.alertThreshold ?? 1000) })
      } else setCfg({ alertThreshold: 1000 })
    } catch { setCfg({ alertThreshold: 1000 }) }
  }

  useEffect(() => { loadTx(); loadCfg(); }, [refreshTick])

  useEffect(() => {
    const onTxUpdated = () => setRefreshTick(t => t + 1)
    const onCfgUpdated = () => setRefreshTick(t => t + 1)
    const onVisible = () => { if (document.visibilityState === 'visible') setRefreshTick(t => t + 1) }
    window.addEventListener('ktp:tx-updated', onTxUpdated as any)
    window.addEventListener('ktp:karat-updated', onCfgUpdated as any)
    document.addEventListener('visibilitychange', onVisible)
    const h = setInterval(() => setRefreshTick(t => t + 1), 30000)
    return () => {
      window.removeEventListener('ktp:tx-updated', onTxUpdated as any)
      window.removeEventListener('ktp:karat-updated', onCfgUpdated as any)
      document.removeEventListener('visibilitychange', onVisible)
      clearInterval(h)
    }
  }, [])

  const diffs = useMemo(() => {
    const rows = summary?.karatRows ?? []
    const row22 = rows.find((x) => x.ayar === 22)
    const row24 = rows.find((x) => x.ayar === 24)
    const diff22 = row22 ? Math.max(0, Number(row22.inv) - Number(row22.exp)) : 0
    const diff24 = row24 ? Math.max(0, Number(row24.inv) - Number(row24.exp)) : 0
    return { diff22, diff24 }
  }, [summary])

  const thr = cfg?.alertThreshold || 0
  const trig22 = diffs.diff22 > thr
  const trig24 = diffs.diff24 > thr
  const show = trig22 || trig24
  if (!show) return null
  const parts: string[] = []
  if (trig22) parts.push(`22 Ayar (${diffs.diff22.toLocaleString('tr-TR', { maximumFractionDigits: 2 })} gr)`)
  if (trig24) parts.push(`24 Ayar (${diffs.diff24.toLocaleString('tr-TR', { maximumFractionDigits: 2 })} gr)`)
  const which = parts.join(' ve ')
  return (
    <div className="border-b bg-amber-100 border-amber-200 dark:bg-amber-950 dark:border-amber-900">
      <div className="px-4 py-2 text-sm text-amber-900 dark:text-amber-200">
        Dikkat! {which} icin faturalanan altin ile gider altini arasinda fark var. Gider kesiniz.
      </div>
    </div>
  )
}
