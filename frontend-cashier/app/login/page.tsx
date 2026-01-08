"use client"
import { useEffect, useState } from 'react'
import { useRouter } from 'next/navigation'
import { api } from '../../lib/api'

export default function LoginPage() {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const router = useRouter()

  useEffect(() => {
    try {
      const token = localStorage.getItem('ktp_c_token')
      if (token) router.replace('/')
    } catch {}
  }, [router])

  // Always read live form values so password manager/browser autofill works
  async function onSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault()
    const fd = new FormData(e.currentTarget)
    const emailVal = (fd.get('email')?.toString().trim() || email).trim()
    const passwordVal = fd.get('password')?.toString() || password
    setLoading(true)
    setError(null)
    try {
      const resp = await api.login(emailVal, passwordVal)
      localStorage.setItem('ktp_c_token', resp.token)
      localStorage.setItem('ktp_c_role', resp.role)
      localStorage.setItem('ktp_c_email', resp.email)
      document.cookie = `ktp_c_token=${resp.token}; Max-Age=${60 * 60 * 8}; Path=/`
      try {
        window.dispatchEvent(new CustomEvent('ktp:auth-changed'))
      } catch {}
      try { sessionStorage.setItem('ktp_c_login_success', '1') } catch {}
      window.location.assign('/')
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
    <main style={{ display: 'flex', minHeight: '70vh', alignItems: 'center', justifyContent: 'center' }}>
      <div className="card" style={{ width: '100%', maxWidth: 420 }}>
        <h1 style={{ marginBottom: 12 }}>Kasiyer Girişi</h1>
        <form onSubmit={onSubmit} className="form-stack">
          <div className="form-row">
            <label htmlFor="email">Email</label>
            <input
              id="email"
              name="email"
              autoComplete="email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
            />
          </div>
          <div className="form-row">
            <label htmlFor="password">Şifre</label>
            <input
              id="password"
              name="password"
              autoComplete="current-password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
          </div>
          {error && <div style={{ color: '#b3261e', fontSize: 14 }}>{error}</div>}
          <div className="actions" style={{ justifyContent: 'flex-end' }}>
            <button type="submit" disabled={loading} className="primary" style={{ minWidth: 140 }}>
              {loading ? 'Giriş yapılıyor...' : 'Giriş Yap'}
            </button>
          </div>
        </form>
      </div>
    </main>
  )
}
