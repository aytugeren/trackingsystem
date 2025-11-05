"use client"
import { useEffect, useState, type ChangeEvent } from 'react'
import { listUsersWithPermissions, updateUserPermissions, type UserWithPermissions, listRoles, assignRole, type RoleDef } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'

export default function UserPermissionsPage() {
  const [users, setUsers] = useState<UserWithPermissions[]>([])
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const [role, setRole] = useState<string | null>(null)
  const [roles, setRoles] = useState<RoleDef[]>([])

  async function load() {
    try {
      setError('')
      setLoading(true)
      const u = await listUsersWithPermissions()
      setUsers(u)
      const rs = await listRoles()
      setRoles(rs)
    } catch { setError('Kullanıcılar yüklenemedi') } finally { setLoading(false) }
  }
  useEffect(() => { try { setRole(localStorage.getItem('ktp_role')) } catch {}; load() }, [])

  if (role !== 'Yonetici') return <p className="text-sm text-muted-foreground">Bu sayfa için yetkiniz yok.</p>

  return (
    <Card>
      <CardHeader>
        <CardTitle>Kullanıcı Yetkileri</CardTitle>
      </CardHeader>
      <CardContent>
        {error && <p className="text-red-600 mb-2">{error}</p>}
        {loading ? <p>Yükleniyor…</p> : <UsersTable users={users} roles={roles} onAssign={async (uid, rid) => { await assignRole(uid, rid); await load() }} onChange={async (id, patch) => { await updateUserPermissions(id, patch); await load() }} />}
      </CardContent>
    </Card>
  )
}

function UsersTable({ users, roles, onAssign, onChange }: { users: UserWithPermissions[]; roles: RoleDef[]; onAssign: (userId: string, roleId: string | null) => Promise<void>; onChange: (id: string, patch: Partial<Pick<UserWithPermissions, 'canCancelInvoice' | 'canAccessLeavesAdmin' | 'leaveAllowanceDays'>>) => Promise<void> }) {
  if (!users || users.length === 0) return <p className="text-sm text-muted-foreground">Kullanıcı bulunamadı</p>
  return (
    <div className="overflow-x-auto">
      <table className="min-w-[900px] w-full text-sm">
        <thead>
          <tr className="text-left">
            <th className="p-2">Email</th>
            <th className="p-2">Rol</th>
            <th className="p-2">Eğlenceli Rol</th>
            <th className="p-2">İptal Et</th>
            <th className="p-2">İzin Yönetimi</th>
            <th className="p-2">Yıllık Hak</th>
            <th className="p-2">Kaydet</th>
          </tr>
        </thead>
        <tbody>
          {users.map(u => <UserRow key={u.id} user={u} roles={roles} onAssign={onAssign} onChange={onChange} />)}
        </tbody>
      </table>
    </div>
  )
}

function UserRow({ user, roles, onAssign, onChange }: { user: UserWithPermissions; roles: RoleDef[]; onAssign: (userId: string, roleId: string | null) => Promise<void>; onChange: (id: string, patch: Partial<Pick<UserWithPermissions, 'canCancelInvoice' | 'canAccessLeavesAdmin' | 'leaveAllowanceDays'>>) => Promise<void> }) {
  const [canCancel, setCanCancel] = useState(user.canCancelInvoice)
  const [canLeaves, setCanLeaves] = useState(user.canAccessLeavesAdmin)
  const [allowance, setAllowance] = useState<number | undefined>(user.leaveAllowanceDays ?? 14)
  const [busy, setBusy] = useState(false)
  const [roleSel, setRoleSel] = useState<string | null>(user.assignedRoleId || null)
  return (
    <tr className="border-t">
      <td className="p-2">{user.email}</td>
      <td className="p-2">{user.customRoleName || user.role}</td>
      <td className="p-2">
        <div className="flex items-center gap-2">
          <select className="border rounded px-2 py-1" value={roleSel || ''} onChange={e => setRoleSel(e.target.value || null)}>
            <option value="">(Yok)</option>
            {roles.map(r => <option key={r.id} value={r.id}>{r.name}</option>)}
          </select>
          <Button size="sm" variant="outline" onClick={async () => { setBusy(true); await onAssign(user.id, roleSel); setBusy(false) }}>Uygula</Button>
        </div>
      </td>
      <td className="p-2"><input type="checkbox" checked={canCancel} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanCancel(e.target.checked)} /></td>
      <td className="p-2"><input type="checkbox" checked={canLeaves} onChange={(e: ChangeEvent<HTMLInputElement>) => setCanLeaves(e.target.checked)} /></td>
      <td className="p-2"><input type="number" className="w-24 border rounded px-2 py-1" value={allowance} onChange={(e: ChangeEvent<HTMLInputElement>) => setAllowance(parseInt(e.target.value || '0', 10))} /></td>
      <td className="p-2"><Button size="sm" disabled={busy} onClick={async () => { setBusy(true); await onChange(user.id, { canCancelInvoice: canCancel, canAccessLeavesAdmin: canLeaves, leaveAllowanceDays: allowance }); setBusy(false) }}>Kaydet</Button></td>
    </tr>
  )
}
