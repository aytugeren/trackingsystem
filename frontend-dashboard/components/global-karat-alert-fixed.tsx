"use client"
import { useEffect, useMemo, useState } from 'react'
import { api, type Expense, type Invoice } from '@/lib/api'

export default function GlobalKaratAlertFixed() {
  const [invoices, setInvoices] = useState<Invoice[] | null>(null)
  const [expenses, setExpenses] = useState<Expense[] | null>(null)
  const [cfg, setCfg] = useState<{ alertThreshold: number } | null>(null)
  const [refreshTick, setRefreshTick] = useState(0)

  const loadTx = async () => {
    try {
      const [inv, exp] = await Promise.all([api.listAllInvoices(), api.listAllExpenses()])
      setInvoices(inv); setExpenses(exp)
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

  const toAyar = (v: Invoice['altinAyar'] | Expense['altinAyar']): 22 | 24 | null => (v===22||v==='Ayar22')?22:(v===24||v==='Ayar24')?24:null
  const diffs = useMemo(() => {
    const invs = (invoices||[]).filter(x => (x.kesildi ?? false))
    const exps = (expenses||[]).filter(x => (x.kesildi ?? false))
    let inv22=0, inv24=0, exp22=0, exp24=0
    for (const i of invs) { const a = toAyar(i.altinAyar); if (a===22) inv22 += Number(i.gramDegeri ?? 0); else if (a===24) inv24 += Number(i.gramDegeri ?? 0) }
    for (const e of exps) { const a = toAyar(e.altinAyar); if (a===22) exp22 += Number(e.gramDegeri ?? 0); else if (a===24) exp24 += Number(e.gramDegeri ?? 0) }
    return { diff22: Math.max(0, inv22-exp22), diff24: Math.max(0, inv24-exp24) }
  }, [invoices, expenses])

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
