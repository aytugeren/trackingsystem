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

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault()
    setLoading(true)
    setError(null)
    try {
      const resp = await api.login(email, password)
      localStorage.setItem('ktp_c_token', resp.token)
      localStorage.setItem('ktp_c_role', resp.role)
      localStorage.setItem('ktp_c_email', resp.email)
      document.cookie = `ktp_c_token=${resp.token}; Max-Age=${60*60*8}; Path=/`
      try { window.dispatchEvent(new CustomEvent('ktp:auth-changed')) } catch {}
      router.push('/')
    } catch (err) {
      setError('Giriş başarısız. Bilgileri kontrol edin.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <main style={{ display: 'flex', minHeight: '70vh', alignItems: 'center', justifyContent: 'center' }}>
      <div className="card" style={{ width: '100%', maxWidth: 420 }}>
        <h1 style={{ marginBottom: 12 }}>Kasiyer Girişi</h1>
        <form onSubmit={onSubmit} className="form-stack">
          <div className="form-row">
            <label htmlFor="email">Email</label>
            <input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
          </div>
          <div className="form-row">
            <label htmlFor="password">Şifre</label>
            <input id="password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
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
