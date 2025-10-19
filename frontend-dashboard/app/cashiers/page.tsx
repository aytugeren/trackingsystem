"use client"
import { useEffect, useState } from 'react'
import { adminApi } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Button } from '@/components/ui/button'

type User = { id: string; email: string; role: string }

export default function CashiersPage() {
  const [list, setList] = useState<User[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [creating, setCreating] = useState(false)

  async function load() {
    setError(null)
    try {
      const users = await adminApi.listUsers('Kasiyer')
      setList(users)
    } catch (e: any) {
      setError(e?.message || 'Yüklenemedi')
    }
  }

  useEffect(() => { setLoading(true); load().finally(() => setLoading(false)) }, [])

  async function onCreate(e: React.FormEvent) {
    e.preventDefault()
    setCreating(true)
    setError(null)
    try {
      await adminApi.createUser(email.trim(), password, 'Kasiyer')
      setEmail(''); setPassword('')
      await load()
    } catch (e: any) {
      setError(e?.message || 'Oluşturulamadı')
    } finally {
      setCreating(false)
    }
  }

  async function onResetPassword(id: string) {
    const pwd = prompt('Yeni şifreyi giriniz:')
    if (!pwd) return
    try {
      await adminApi.resetUserPassword(id, pwd)
      alert('Şifre güncellendi')
    } catch (e: any) {
      alert(e?.message || 'Şifre güncellenemedi')
    }
  }

  return (
    <div className="space-y-4">
      <Card>
        <CardHeader>
          <CardTitle>Kasiyer Ekle</CardTitle>
        </CardHeader>
        <CardContent>
          <form className="grid gap-3 md:grid-cols-3" onSubmit={onCreate}>
            <div className="space-y-1">
              <Label htmlFor="email">Email</Label>
              <Input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
            </div>
            <div className="space-y-1">
              <Label htmlFor="password">Şifre</Label>
              <Input id="password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
            </div>
            <div className="flex items-end">
              <Button type="submit" disabled={creating}>Ekle</Button>
            </div>
          </form>
          {error && <div className="text-sm text-red-600 mt-2">{error}</div>}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Kasiyerler</CardTitle>
        </CardHeader>
        <CardContent>
          {loading ? (
            <div>Yükleniyor...</div>
          ) : list.length === 0 ? (
            <div className="text-sm text-muted-foreground">Kayıt yok</div>
          ) : (
            <div className="space-y-2">
              {list.map((u) => (
                <div key={u.id} className="flex items-center justify-between rounded-md border p-2">
                  <div>
                    <div className="font-medium">{u.email}</div>
                    <div className="text-xs text-muted-foreground">{u.role}</div>
                  </div>
                  <Button variant="outline" onClick={() => onResetPassword(u.id)}>Şifre Sıfırla</Button>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  )
}

