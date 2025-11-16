"use client"
import { useEffect, useMemo, useState, useCallback } from 'react'
import { listLeavesAdmin, updateLeaveStatus, listLeaveSummary, setUserLeaveAllowance, updateUserPermissions, type Leave, type LeaveSummaryItem } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'

function fmt(d: string) { return new Date(d).toLocaleDateString('tr-TR') }

export default function LeavesAdminPage() {
  const [from, setFrom] = useState<string>('')
  const [to, setTo] = useState<string>('')
  const [leaves, setLeaves] = useState<Leave[] | null>(null)
  const [summary, setSummary] = useState<{ year: number; items: LeaveSummaryItem[] } | null>(null)
  const [year, setYear] = useState<number>(new Date().getFullYear())
  const [error, setError] = useState<string>('')
  const [perms, setPerms] = useState<{ canAccessLeavesAdmin?: boolean } | null>(null)

  const load = useCallback(async () => {
    try {
      setError('')
      const [ls, sm] = await Promise.all([
        listLeavesAdmin({ from: from || undefined, to: to || undefined }),
        listLeaveSummary(year)
      ])
      setLeaves(ls)
      setSummary(sm)
    } catch (e) { setError('Veri yüklenemedi') }
  }, [from, to, year])

  useEffect(() => {
    ;(async () => { try { const base = process.env.NEXT_PUBLIC_API_BASE || ''; const token = typeof window !== 'undefined' ? (localStorage.getItem('ktp_token') || '') : ''; const res = await fetch(`${base}/api/me/permissions`, { cache: 'no-store', headers: token ? { Authorization: `Bearer ${token}` } : {} }); if (res.ok) setPerms(await res.json()) } catch {} })()
    load()
  }, [year, load])

  const byStatus = useMemo(() => {
    const map: Record<string, Leave[]> = { Pending: [], Approved: [], Rejected: [] }
    for (const l of (leaves || [])) { (map[l.status] ||= []).push(l) }
    return map
  }, [leaves])

  if (!perms?.canAccessLeavesAdmin) {
    return <p className="text-sm text-muted-foreground">Bu sayfa için yetkiniz yok.</p>
  }

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>İzin Yönetimi</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
            <div className="flex gap-2 items-end">
              <div className="flex flex-col">
                <label className="text-sm text-muted-foreground">Başlangıç</label>
                <input type="date" value={from} onChange={e => setFrom(e.target.value)} className="border rounded px-2 py-1 text-slate-900" />
              </div>
              <div className="flex flex-col">
                <label className="text-sm text-muted-foreground">Bitiş</label>
                <input type="date" value={to} onChange={e => setTo(e.target.value)} className="border rounded px-2 py-1 text-slate-900" />
              </div>
              <Button onClick={load}>Filtrele</Button>
            </div>
            <div className="flex gap-2 items-center">
              <label className="text-sm">Yıl:</label>
              <input type="number" value={year} onChange={e => setYear(parseInt(e.target.value||`${new Date().getFullYear()}`,10))} className="w-24 border rounded px-2 py-1 text-slate-900" />
            </div>
          </div>
          {error && <p className="text-red-600 mt-2">{error}</p>}
        </CardContent>
      </Card>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Bekleyen İzinler</CardTitle>
          </CardHeader>
          <CardContent>
            <LeavesTable data={(byStatus['Pending']||[])} onAction={async (id, s) => { await updateLeaveStatus(id, s); await load() }} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Onaylanan / Reddedilen</CardTitle>
          </CardHeader>
          <CardContent>
            <LeavesTable data={[...(byStatus['Approved']||[]), ...(byStatus['Rejected']||[])]} />
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Yıllık Özet ve İzin Hakkı</CardTitle>
        </CardHeader>
        <CardContent>
          {summary && <SummaryTable data={summary.items} onSetAllowance={async (uid, days) => { await setUserLeaveAllowance(uid, days); await load() }} />}
        </CardContent>
      </Card>
    </div>
  )
}

function Badge({ color }: { color: 'green'|'amber'|'red' }) {
  const clr = color==='green'? 'bg-green-500' : color==='amber' ? 'bg-amber-500' : 'bg-red-500'
  return <span className={`inline-block w-2.5 h-2.5 rounded-full ${clr}`} />
}

function LeavesTable({ data, onAction }: { data: Leave[]; onAction?: (id: string, status: 'Pending'|'Approved'|'Rejected') => Promise<void> }) {
  if (!data || data.length === 0) return <p className="text-sm text-muted-foreground">Kayıt yok</p>
  return (
    <div className="overflow-x-auto">
      <table className="min-w-[640px] w-full text-sm">
        <thead>
          <tr className="text-left">
            <th className="p-2">Tarih</th>
            <th className="p-2">Kişi</th>
            <th className="p-2">Durum</th>
            <th className="p-2">Açıklama</th>
            <th className="p-2">{onAction ? 'İşlem' : 'Durum'}</th>
          </tr>
        </thead>
        <tbody>
          {data.map(l => (
            <tr key={l.id} className="border-t">
              <td className="p-2 whitespace-nowrap">{fmt(l.from)} - {fmt(l.to)}{(l.fromTime && l.toTime && l.from === l.to) ? ` (${l.fromTime}-${l.toTime})` : ''}</td>
              <td className="p-2">{l.user}</td>
              <td className="p-2">
                {l.status === 'Approved' && <Badge color="green" />} 
                {l.status === 'Pending' && <Badge color="amber" />} 
                {l.status === 'Rejected' && <Badge color="red" />}
              </td>
              <td className="p-2 max-w-[280px] truncate" title={l.reason || ''}>{l.reason || '-'}</td>
              <td className="p-2">
                {onAction ? (
                  <div className="flex gap-2">
                    <Button size="sm" variant="default" onClick={() => onAction(l.id, 'Approved')!}>Onayla</Button>
                    <Button size="sm" variant="outline" className="border-red-300 text-red-600 hover:bg-red-50" onClick={() => onAction(l.id, 'Rejected')!}>Reddet</Button>
                    <Button size="sm" variant="outline" className="text-muted-foreground" onClick={() => onAction(l.id, 'Pending')!}>Beklet</Button>
                  </div>
                ) : (
                  <span className="text-xs text-muted-foreground">{l.status === 'Approved' ? 'Onaylandı' : l.status === 'Rejected' ? 'Reddedildi' : 'Bekliyor'}</span>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function SummaryTable({ data, onSetAllowance }: { data: LeaveSummaryItem[]; onSetAllowance: (userId: string, days: number) => Promise<void> }) {
  const [editing, setEditing] = useState<Record<string, number>>({})
  return (
    <div className="overflow-x-auto">
      <table className="min-w-[640px] w-full text-sm">
        <thead>
          <tr className="text-left">
            <th className="p-2">Kullanıcı</th>
            <th className="p-2">Kullanılan (gün)</th>
            <th className="p-2">Hakkı (gün)</th>
            <th className="p-2">Kalan (gün)</th>
            <th className="p-2">Güncelle</th>
          </tr>
        </thead>
        <tbody>
          {data.map(x => (
            <tr key={x.userId} className="border-t">
              <td className="p-2">{x.email}</td>
              <td className="p-2">{Number(x.usedDays).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
              <td className="p-2">{x.allowanceDays ?? 14}</td>
              <td className="p-2">
                <div>{Number(x.remainingDays).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</div>
                <div className="text-xs text-muted-foreground">{(() => { const full = Math.max(0, Math.floor(Number(x.remainingDays))); const rem = Math.max(0, Math.round((Number(x.remainingDays) - full) * 8)); return `${full} gün ${rem} saat`; })()}</div>
              </td>
              <td className="p-2">
                <div className="flex gap-2 items-center">
                  <input type="number" className="w-24 border rounded px-2 py-1 text-slate-900" value={editing[x.userId] ?? (x.allowanceDays ?? 14)} onChange={(e) => setEditing({ ...editing, [x.userId]: parseInt(e.target.value || '0', 10) })} />
                  <Button size="sm" onClick={async () => { const v = editing[x.userId] ?? (x.allowanceDays ?? 14); await onSetAllowance(x.userId, v); }}>Kaydet</Button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}


