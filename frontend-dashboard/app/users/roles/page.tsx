"use client"
import { useEffect, useState, type ChangeEvent } from 'react'
import { listRoles, createRole, updateRole, deleteRole, type RoleDef } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'

export default function RolesPage() {
  const [roles, setRoles] = useState<RoleDef[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [role, setRole] = useState<string | null>(null)

  async function load() {
    try {
      setError('')
      setLoading(true)
      const rs = await listRoles()
      setRoles(rs)
    } catch { setError('Roller yüklenemedi') } finally { setLoading(false) }
  }
  useEffect(() => { try { setRole(localStorage.getItem('ktp_role')) } catch {}; load() }, [])

  if (role !== 'Yonetici') return <p className="text-sm text-muted-foreground">Bu sayfa için yetkiniz yok.</p>

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
              <table className="min-w-[720px] w-full text-sm">
                <thead>
                  <tr className="text-left">
                    <th className="p-2">Ad</th>
                    <th className="p-2">İptal</th>
                    <th className="p-2">İzin Yönetimi</th>
                    <th className="p-2">Yıllık Hak</th>
                    <th className="p-2">Gün/Saat</th>
                    <th className="p-2">İşlem</th>
                  </tr>
                </thead>
                <tbody>
                  {roles.map(r => <RoleRow key={r.id} role={r} onChange={async (patch) => { await updateRole(r.id, patch); await load() }} onDelete={async () => { await deleteRole(r.id); await load() }} />)}
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
  const [canAccessLeavesAdmin, setCanAccessLeavesAdmin] = useState(false)
  const [leaveAllowanceDays, setLeaveAllowanceDays] = useState<number | ''>('')
  const [workingDayHours, setWorkingDayHours] = useState<number | ''>('')
  const [busy, setBusy] = useState(false)
  return (
    <div className="flex flex-wrap items-end gap-3">
      <div className="flex flex-col">
        <label className="text-sm text-muted-foreground">Rol Adı</label>
        <input value={name} onChange={(e: ChangeEvent<HTMLInputElement>) => setName(e.target.value)} className="border rounded px-3 py-2 w-56" placeholder="Yaşlı Uzmanı" />
      </div>
      <label className="flex items-center gap-2"><input type="checkbox" checked={canCancelInvoice} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanCancelInvoice(e.target.checked)} /> İptal Yetkisi</label>
      <label className="flex items-center gap-2"><input type="checkbox" checked={canAccessLeavesAdmin} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanAccessLeavesAdmin(e.target.checked)} /> İzin Yönetimi</label>
      <div className="flex items-center gap-2">
        <span>Yıllık Hak</span>
        <input type="number" value={leaveAllowanceDays} onChange={(e: ChangeEvent<HTMLInputElement>) => setLeaveAllowanceDays(e.target.value === '' ? '' : parseInt(e.target.value, 10))} className="border rounded px-3 py-2 w-24" />
      </div>
      <div className="flex items-center gap-2">
        <span>Gün/Saat</span>
        <input type="number" step="0.1" value={workingDayHours} onChange={(e: ChangeEvent<HTMLInputElement>) => setWorkingDayHours(e.target.value === '' ? '' : parseFloat(e.target.value))} className="border rounded px-3 py-2 w-24" />
      </div>
      <Button disabled={busy || !name.trim()} onClick={async () => { setBusy(true); await createRole({ name: name.trim(), canCancelInvoice, canAccessLeavesAdmin, leaveAllowanceDays: leaveAllowanceDays === '' ? null : leaveAllowanceDays, workingDayHours: workingDayHours === '' ? null : workingDayHours }); setName(''); setCanCancelInvoice(false); setCanAccessLeavesAdmin(false); setLeaveAllowanceDays(''); setWorkingDayHours(''); await onCreated(); setBusy(false) }}>Ekle</Button>
    </div>
  )
}

function RoleRow({ role, onChange, onDelete }: { role: RoleDef; onChange: (patch: Partial<RoleDef>) => Promise<void>; onDelete: () => Promise<void> }) {
  const [name, setName] = useState(role.name)
  const [canCancelInvoice, setCanCancelInvoice] = useState(role.canCancelInvoice)
  const [canAccessLeavesAdmin, setCanAccessLeavesAdmin] = useState(role.canAccessLeavesAdmin)
  const [leaveAllowanceDays, setLeaveAllowanceDays] = useState<number | ''>(role.leaveAllowanceDays ?? '')
  const [workingDayHours, setWorkingDayHours] = useState<number | ''>(role.workingDayHours ?? '')
  const [busy, setBusy] = useState(false)
  return (
    <tr className="border-t">
      <td className="p-2"><input value={name} onChange={(e: ChangeEvent<HTMLInputElement>) => setName(e.target.value)} className="border rounded px-2 py-1 w-56" /></td>
      <td className="p-2"><input type="checkbox" checked={canCancelInvoice} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanCancelInvoice(e.target.checked)} /></td>
      <td className="p-2"><input type="checkbox" checked={canAccessLeavesAdmin} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanAccessLeavesAdmin(e.target.checked)} /></td>
      <td className="p-2"><input type="number" className="w-24 border rounded px-2 py-1" value={leaveAllowanceDays} onChange={(e: ChangeEvent<HTMLInputElement>) => setLeaveAllowanceDays(e.target.value === '' ? '' : parseInt(e.target.value, 10))} /></td>
      <td className="p-2"><input type="number" step="0.1" className="w-24 border rounded px-2 py-1" value={workingDayHours} onChange={(e: ChangeEvent<HTMLInputElement>) => setWorkingDayHours(e.target.value === '' ? '' : parseFloat(e.target.value))} /></td>
      <td className="p-2 flex gap-2">
        <Button size="sm" disabled={busy} onClick={async () => { setBusy(true); await onChange({ name, canCancelInvoice, canAccessLeavesAdmin, leaveAllowanceDays: leaveAllowanceDays === '' ? null : leaveAllowanceDays, workingDayHours: workingDayHours === '' ? null : workingDayHours }); setBusy(false) }}>Kaydet</Button>
        <Button size="sm" variant="outline" disabled={busy} onClick={async () => { setBusy(true); await onDelete(); setBusy(false) }}>Sil</Button>
      </td>
    </tr>
  )
}
