"use client"
import { useEffect, useMemo, useState } from 'react'

type ListItem = {
  tarih?: string
  altinAyar?: number | 'Ayar22' | 'Ayar24'
  gramDegeri?: number
  kesildi?: boolean
}

type ListResponse = { items: ListItem[]; totalCount: number }

export default function GlobalKaratAlert() {
  const [inv, setInv] = useState<ListItem[] | null>(null)
  const [exp, setExp] = useState<ListItem[] | null>(null)
  const [thr, setThr] = useState<number | null>(null)
  const [authed, setAuthed] = useState<boolean>(() => {
    try { return !!localStorage.getItem('ktp_c_token') } catch { return false }
  })

  const apiBase = process.env.NEXT_PUBLIC_API_URL || ''

  async function loadAll() {
    const token = typeof window !== 'undefined' ? (localStorage.getItem('ktp_c_token') || '') : ''
    if (!token) {
      setInv(null); setExp(null); setThr(null)
      return
    }
    const headers: HeadersInit = { Authorization: `Bearer ${token}` }
    try {
      const [invRes, expRes, cfgRes] = await Promise.all([
        fetch(`${apiBase}/api/invoices?page=1&pageSize=500`, { cache: 'no-store', headers }),
        fetch(`${apiBase}/api/expenses?page=1&pageSize=500`, { cache: 'no-store', headers }),
        fetch(`${apiBase}/api/settings/karat`, { cache: 'no-store', headers }),
      ])
      if (invRes.ok) {
        const j: ListResponse = await invRes.json(); setInv(j.items || [])
      } else setInv([])
      if (expRes.ok) {
        const j: ListResponse = await expRes.json(); setExp(j.items || [])
      } else setExp([])
      if (cfgRes.ok) {
        const j = await cfgRes.json(); setThr(Number(j?.alertThreshold ?? 1000))
      } else setThr(1000)
    } catch {
      // graceful fallbacks
      setInv([]); setExp([]); setThr(1000)
    }
  }

  // Kick initial load, and re-poll
  useEffect(() => {
    loadAll()
    const h = setInterval(loadAll, 30000)
    return () => clearInterval(h)
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // React immediately to login/logout within the same tab
  useEffect(() => {
    const onAuthChanged = () => {
      const has = !!localStorage.getItem('ktp_c_token')
      setAuthed(has)
      // Refresh immediately when token appears/disappears
      loadAll()
    }
    const onStorage = (e: StorageEvent) => { if (e.key === 'ktp_c_token') onAuthChanged() }
    window.addEventListener('ktp:auth-changed', onAuthChanged as EventListener)
    window.addEventListener('storage', onStorage)
    return () => {
      window.removeEventListener('ktp:auth-changed', onAuthChanged as EventListener)
      window.removeEventListener('storage', onStorage)
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const monthKey = (() => { const n = new Date(); return `${n.getFullYear()}-${String(n.getMonth()+1).padStart(2,'0')}` })()
  const toAyar = (v: ListItem['altinAyar']): 22 | 24 | null => (v===22||v==='Ayar22')?22:(v===24||v==='Ayar24')?24:null
  const diffs = useMemo(() => {
    const invs = (inv||[]).filter(x => (x.kesildi ?? false) && (x.tarih||'').startsWith(monthKey))
    const exps = (exp||[]).filter(x => (x.kesildi ?? false) && (x.tarih||'').startsWith(monthKey))
    let inv22=0, inv24=0, exp22=0, exp24=0
    for (const i of invs) { const a = toAyar(i.altinAyar); if (a===22) inv22 += Number(i.gramDegeri||0); else if (a===24) inv24 += Number(i.gramDegeri||0) }
    for (const e of exps) { const a = toAyar(e.altinAyar); if (a===22) exp22 += Number(e.gramDegeri||0); else if (a===24) exp24 += Number(e.gramDegeri||0) }
    return { diff22: Math.max(0, inv22-exp22), diff24: Math.max(0, inv24-exp24) }
  }, [inv, exp, monthKey])

  const threshold = thr ?? 0
  const trig22 = diffs.diff22 > threshold
  const trig24 = diffs.diff24 > threshold
  const show = trig22 || trig24
  const [hidden, setHidden] = useState(false)
  useEffect(() => { if (show) setHidden(false) }, [show])
  if (!authed || !show || hidden) return null

  const parts: string[] = []
  if (trig22) parts.push(`22 Ayar (${diffs.diff22.toLocaleString('tr-TR', { maximumFractionDigits: 2 })} gr)`)
  if (trig24) parts.push(`24 Ayar (${diffs.diff24.toLocaleString('tr-TR', { maximumFractionDigits: 2 })} gr)`)
  const which = parts.join(' ve ')

  return (
    <div style={{ borderBottom: '1px solid #fcd34d', background: '#fef3c7', color: '#78350f', fontSize: 13 }}>
      <div style={{ padding: '8px 12px', display: 'flex', alignItems: 'flex-start', gap: 12 }}>
        <div aria-hidden style={{ marginTop: 2 }}>
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M12 9v4"/><path d="M12 17h.01"/><path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0Z"/></svg>
        </div>
        <div style={{ minWidth: 0, flex: 1 }}>
          Dikkat! {which} için faturalanan altın ile gider altını arasında fark var. Gider kesiniz.
        </div>
        <button onClick={() => setHidden(true)} style={{ marginLeft: 'auto', height: 24, padding: '0 8px', borderRadius: 6, border: '1px solid #eab308', background: 'transparent', fontSize: 12, color: '#78350f' }}>
          Gizle
        </button>
      </div>
    </div>
  )
}
