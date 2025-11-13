"use client"
import { useEffect, useState, type ChangeEvent } from 'react'
import { listRoles, createRole, updateRole, deleteRole, type RoleDef } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'

export default function RolesPage() {
  const [roles, setRoles] = useState<RoleDef[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [allowed, setAllowed] = useState(false)

  async function load() {
    try {
      setError('')
      setLoading(true)
      const rs = await listRoles()
      setRoles(rs)
    } catch { setError('Roller yüklenemedi') } finally { setLoading(false) }
  }
  useEffect(() => {
    load()
    ;(async () => {
      try {
        const base = process.env.NEXT_PUBLIC_API_BASE || ''
        const token = typeof window !== 'undefined' ? (localStorage.getItem('ktp_token') || '') : ''
        const res = await fetch(`${base}/api/me/permissions`, { cache: 'no-store', headers: token ? { Authorization: `Bearer ${token}` } : {} })
        if (res.ok) { const j = await res.json(); setAllowed(Boolean(j?.canManageCashier) || String(j?.role) === 'Yonetici') }
      } catch {}
    })()
  }, [])

  if (!allowed) return <p className="text-sm text-muted-foreground">Bu sayfa için yetkiniz yok.</p>

  return (
    <Card>
      <CardHeader>
        <CardTitle>Roller</CardTitle>
      </CardHeader>
      <CardContent>
        {error && <p className="text-red-600 mb-2">{error}</p>}
        {loading ? <p>Yükleniyor…</p> : (
          <div className="space-y-4">
            <NewRoleForm onCreated={load} />
            <div className="overflow-x-auto">
              <table className="min-w-[920px] w-full text-sm">
                <thead>
            <tr className="text-left">
              <th className="p-2">Ad</th>
              <th className="p-2">İptal</th>
              <th className="p-2">Kesildi</th>
              <th className="p-2">İzin Yönetimi</th>
              <th className="p-2">Ayar Yönetimi</th>
              <th className="p-2">Kasiyer Yönetimi</th>
              <th className="p-2">Karat</th>
              <th className="p-2">Fatura</th>
              <th className="p-2">Gider</th>
              <th className="p-2">Rapor</th>
              <th className="p-2">Etiket</th>
              <th className="p-2">Yıllık Hak</th>
              <th className="p-2">Gün/Saat</th>
              <th className="p-2">İşlem</th>
            </tr>
                </thead>
                <tbody>
                  {roles.map(r => (
                    <RoleRow
                      key={r.id}
                      role={r}
                      onChange={async (patch) => { await updateRole(r.id, patch); await load() }}
                      onDelete={async () => { await deleteRole(r.id); await load() }}
                    />
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  )
}

function NewRoleForm({ onCreated }: { onCreated: () => Promise<void> }) {
  const [name, setName] = useState('')
  const [canCancelInvoice, setCanCancelInvoice] = useState(false)
  const [canToggleKesildi, setCanToggleKesildi] = useState(false)
  const [canAccessLeavesAdmin, setCanAccessLeavesAdmin] = useState(false)
  const [canManageSettings, setCanManageSettings] = useState(false)
  const [canManageCashier, setCanManageCashier] = useState(false)
  const [canManageKarat, setCanManageKarat] = useState(false)
  const [canUseInvoices, setCanUseInvoices] = useState(false)
  const [canUseExpenses, setCanUseExpenses] = useState(false)
  const [canViewReports, setCanViewReports] = useState(false)
  const [canPrintLabels, setCanPrintLabels] = useState(false)
  const [leaveAllowanceDays, setLeaveAllowanceDays] = useState<number | ''>('')
  const [workingDayHours, setWorkingDayHours] = useState<number | ''>('')
  const [busy, setBusy] = useState(false)
  return (
    <div className="flex flex-wrap items-end gap-3">
      <div className="flex flex-col">
        <label className="text-sm text-muted-foreground">Rol Adı</label>
        <input value={name} onChange={(e: ChangeEvent<HTMLInputElement>) => setName(e.target.value)} className="border rounded px-3 py-2 w-56" placeholder="Rol Giriniz..." />
      </div>
      <label className="flex items-center gap-2"><input type="checkbox" checked={canCancelInvoice} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanCancelInvoice(e.target.checked)} /> İptal</label>
      <label className="flex items-center gap-2"><input type="checkbox" checked={canToggleKesildi} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanToggleKesildi(e.target.checked)} /> Kesildi</label>
      <label className="flex items-center gap-2"><input type="checkbox" checked={canAccessLeavesAdmin} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanAccessLeavesAdmin(e.target.checked)} /> İzin</label>
      <label className="flex items-center gap-2"><input type="checkbox" checked={canManageSettings} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanManageSettings(e.target.checked)} /> Ayarlar</label>
      <label className="flex items-center gap-2"><input type="checkbox" checked={canManageCashier} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanManageCashier(e.target.checked)} /> Kasiyer Yönetimi</label>
      <label className="flex items-center gap-2"><input type="checkbox" checked={canManageKarat} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanManageKarat(e.target.checked)} /> Karat</label>
      <label className="flex items-center gap-2"><input type="checkbox" checked={canUseInvoices} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanUseInvoices(e.target.checked)} /> Fatura</label>
      <label className="flex items-center gap-2"><input type="checkbox" checked={canUseExpenses} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanUseExpenses(e.target.checked)} /> Gider</label>
      <label className="flex items-center gap-2"><input type="checkbox" checked={canViewReports} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanViewReports(e.target.checked)} /> Rapor</label>
      <label className="flex items-center gap-2"><input type="checkbox" checked={canPrintLabels} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanPrintLabels(e.target.checked)} /> Etiket</label>
      <div className="flex items-center gap-2">
        <span>Yıllık Hak</span>
        <input type="number" value={leaveAllowanceDays} onChange={(e: ChangeEvent<HTMLInputElement>) => setLeaveAllowanceDays(e.target.value === '' ? '' : parseInt(e.target.value, 10))} className="border rounded px-3 py-2 w-24" />
      </div>
      <div className="flex items-center gap-2">
        <span>Gün/Saat</span>
        <input type="number" step="0.1" value={workingDayHours} onChange={(e: ChangeEvent<HTMLInputElement>) => setWorkingDayHours(e.target.value === '' ? '' : parseFloat(e.target.value))} className="border rounded px-3 py-2 w-24" />
      </div>
        <Button disabled={busy || !name.trim()} onClick={async () => { setBusy(true); await createRole({ name: name.trim(), canCancelInvoice, canToggleKesildi, canAccessLeavesAdmin, canManageSettings, canManageCashier, canManageKarat, canUseInvoices, canUseExpenses, canViewReports, canPrintLabels, leaveAllowanceDays: leaveAllowanceDays === '' ? null : leaveAllowanceDays, workingDayHours: workingDayHours === '' ? null : workingDayHours }); setName(''); setCanCancelInvoice(false); setCanToggleKesildi(false); setCanAccessLeavesAdmin(false); setCanManageSettings(false); setCanManageCashier(false); setCanManageKarat(false); setCanUseInvoices(false); setCanUseExpenses(false); setCanViewReports(false); setCanPrintLabels(false); setLeaveAllowanceDays(''); setWorkingDayHours(''); await onCreated(); setBusy(false) }}>Ekle</Button>
    </div>
  )
}

function RoleRow({ role, onChange, onDelete }: { role: RoleDef; onChange: (patch: Partial<RoleDef>) => Promise<void>; onDelete: () => Promise<void> }) {
  const [name, setName] = useState(role.name)
  const [canCancelInvoice, setCanCancelInvoice] = useState(role.canCancelInvoice)
  const [canToggleKesildi, setCanToggleKesildi] = useState(role.canToggleKesildi)
  const [canAccessLeavesAdmin, setCanAccessLeavesAdmin] = useState(role.canAccessLeavesAdmin)
  const [canManageSettings, setCanManageSettings] = useState(role.canManageSettings)
  const [canManageCashier, setCanManageCashier] = useState(role.canManageCashier)
  const [canManageKarat, setCanManageKarat] = useState(role.canManageKarat)
  const [canUseInvoices, setCanUseInvoices] = useState(role.canUseInvoices)
  const [canUseExpenses, setCanUseExpenses] = useState(role.canUseExpenses)
  const [canViewReports, setCanViewReports] = useState(role.canViewReports)
  const [canPrintLabels, setCanPrintLabels] = useState(role.canPrintLabels)
  const [leaveAllowanceDays, setLeaveAllowanceDays] = useState<number | ''>(role.leaveAllowanceDays ?? '')
  const [workingDayHours, setWorkingDayHours] = useState<number | ''>(role.workingDayHours ?? '')
  const [busy, setBusy] = useState(false)
  return (
    <tr className="border-t">
      <td className="p-2"><input value={name} onChange={(e: ChangeEvent<HTMLInputElement>) => setName(e.target.value)} className="border rounded px-2 py-1 w-56" /></td>
      <td className="p-2"><input type="checkbox" checked={canCancelInvoice} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanCancelInvoice(e.target.checked)} /></td>
      <td className="p-2"><input type="checkbox" checked={canToggleKesildi} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanToggleKesildi(e.target.checked)} /></td>
      <td className="p-2"><input type="checkbox" checked={canAccessLeavesAdmin} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanAccessLeavesAdmin(e.target.checked)} /></td>
      <td className="p-2"><input type="checkbox" checked={canManageSettings} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanManageSettings(e.target.checked)} /></td>
      <td className="p-2"><input type="checkbox" checked={canManageCashier} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanManageCashier(e.target.checked)} /></td>
      <td className="p-2"><input type="checkbox" checked={canManageKarat} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanManageKarat(e.target.checked)} /></td>
      <td className="p-2"><input type="checkbox" checked={canUseInvoices} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanUseInvoices(e.target.checked)} /></td>
      <td className="p-2"><input type="checkbox" checked={canUseExpenses} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanUseExpenses(e.target.checked)} /></td>
      <td className="p-2"><input type="checkbox" checked={canViewReports} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanViewReports(e.target.checked)} /></td>
      <td className="p-2"><input type="checkbox" checked={canPrintLabels} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanPrintLabels(e.target.checked)} /></td>
      <td className="p-2"><input type="number" className="w-24 border rounded px-2 py-1" value={leaveAllowanceDays} onChange={(e: ChangeEvent<HTMLInputElement>) => setLeaveAllowanceDays(e.target.value === '' ? '' : parseInt(e.target.value, 10))} /></td>
      <td className="p-2"><input type="number" step="0.1" className="w-24 border rounded px-2 py-1" value={workingDayHours} onChange={(e: ChangeEvent<HTMLInputElement>) => setWorkingDayHours(e.target.value === '' ? '' : parseFloat(e.target.value))} /></td>
      <td className="p-2 flex gap-2">
        <Button size="sm" disabled={busy} onClick={async () => { setBusy(true); await onChange({ name, canCancelInvoice, canToggleKesildi, canAccessLeavesAdmin, canManageSettings, canManageCashier, canManageKarat, canUseInvoices, canUseExpenses, canViewReports, canPrintLabels, leaveAllowanceDays: leaveAllowanceDays === '' ? null : leaveAllowanceDays, workingDayHours: workingDayHours === '' ? null : workingDayHours }); setBusy(false) }}>Kaydet</Button>
        <Button size="sm" variant="outline" disabled={busy} onClick={async () => { setBusy(true); await onDelete(); setBusy(false) }}>Sil</Button>
      </td>
    </tr>
  )
}
