"use client"
import { useEffect, useState } from 'react'
import { listUsersWithPermissions, type UserWithPermissions, listRoles, assignRole, type RoleDef } from '@/lib/api'
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
        {loading ? <p>Yükleniyor…</p> : <UsersTable users={users} roles={roles} onAssign={async (uid, rid) => { await assignRole(uid, rid); await load() }} />}
      </CardContent>
    </Card>
  )
}

function UsersTable({ users, roles, onAssign }: { users: UserWithPermissions[]; roles: RoleDef[]; onAssign: (userId: string, roleId: string | null) => Promise<void> }) {
  if (!users || users.length === 0) return <p className="text-sm text-muted-foreground">Kullanıcı bulunamadı</p>
  return (
    <div className="overflow-x-auto">
      <table className="min-w-[700px] w-full text-sm">
        <thead>
          <tr className="text-left">
            <th className="p-2">Email</th>
            <th className="p-2">Rol</th>
            <th className="p-2">Rol Atama</th>
            <th className="p-2">Bilgi</th>
          </tr>
        </thead>
        <tbody>
          {users.map(u => <UserRow key={u.id} user={u} roles={roles} onAssign={onAssign} />)}
        </tbody>
      </table>
    </div>
  )
}

function UserRow({ user, roles, onAssign }: { user: UserWithPermissions; roles: RoleDef[]; onAssign: (userId: string, roleId: string | null) => Promise<void> }) {
  const [busy, setBusy] = useState(false)
  const [roleSel, setRoleSel] = useState<string | null>(user.assignedRoleId || null)
  const info = (() => {
    const r = roles.find(x => x.id === (roleSel || user.assignedRoleId || ''))
    if (!r) return 'Rol seçiniz'
    const flags: string[] = []
    if (r.canCancelInvoice) flags.push('İptal Etme')
    if (r.canToggleKesildi) flags.push('Kesildi/Onay Bekliyor')
    if (r.canAccessLeavesAdmin) flags.push('İzin Yönetimi')
    if (r.canManageSettings) flags.push('Ayar Yönetimi')
    if (r.canManageKarat) flags.push('Karat Ayarlama')
    if (r.canUseInvoices) flags.push('Fatura')
    if (r.canUseExpenses) flags.push('Gider')
    if (r.canViewReports) flags.push('Raporlar')
    return flags.length ? flags.join(', ') : 'Yetki yok'
  })()
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
      <td className="p-2"><span title={info} className="inline-flex h-5 w-5 items-center justify-center rounded-full bg-muted text-xs cursor-help">i</span></td>
    </tr>
  )
}
