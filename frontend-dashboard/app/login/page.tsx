"use client"
import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { api } from '@/lib/api'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Button } from '@/components/ui/button'

export default function LoginPage() {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const router = useRouter()
  useEffect(() => {
    try {
      const token = localStorage.getItem('ktp_token')
      if (token) router.replace('/')
    } catch {}
  }, [router])

  async function onSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()
    setLoading(true)
    setError(null)
    try {
      const form = e.currentTarget as HTMLFormElement;
      const emailEl = form.querySelector('#email') as HTMLInputElement | null;
      const passEl = form.querySelector('#password') as HTMLInputElement | null;
      const emailVal = (email && email.length > 0) ? email : (emailEl?.value || '');
      const passwordVal = (password && password.length > 0) ? password : (passEl?.value || '');
      const resp = await api.login(emailVal, passwordVal)
      localStorage.setItem('ktp_token', resp.token)
      localStorage.setItem('ktp_role', resp.role)
      localStorage.setItem('ktp_email', resp.email)
      // also set a cookie copy for middleware/SSR (non-HttpOnly since set on client)
      document.cookie = `ktp_token=${resp.token}; Max-Age=${60*60*8}; Path=/` // 8h
      router.push('/')
    } catch (err) {
      setError('Giriş başarısız. Bilgileri kontrol edin.')
    } finally {
      setLoading(false)
    }
  }

  // Capture potential autofill values shortly after mount
  useEffect(() => {
    const t = setTimeout(() => {
      try {
        const emailEl = document.getElementById('email') as HTMLInputElement | null
        const passEl = document.getElementById('password') as HTMLInputElement | null
        if (emailEl && emailEl.value && !email) setEmail(emailEl.value)
        if (passEl && passEl.value && !password) setPassword(passEl.value)
      } catch {}
    }, 300)
    return () => clearTimeout(t)
  }, [email, password])

  return (
    <div className="flex min-h-[60vh] items-center justify-center">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle>Giriş Yap</CardTitle>
        </CardHeader>
        <CardContent>
          <form className="space-y-4" onSubmit={onSubmit}>
            <div className="space-y-1">
              <Label htmlFor="email">Email</Label>
              <Input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
            </div>
            <div className="space-y-1">
              <Label htmlFor="password">Şifre</Label>
              <Input id="password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
            </div>
            {error && <div className="text-sm text-red-600">{error}</div>}
            <Button type="submit" disabled={loading} className="w-full">{loading ? 'Giriş yapılıyor...' : 'Giriş Yap'}</Button>
          </form>
        </CardContent>
      </Card>
    </div>
  )
}

