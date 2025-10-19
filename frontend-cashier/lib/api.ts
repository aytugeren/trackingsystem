export type LoginResponse = { token: string; role: string; email: string }

const API_BASE = process.env.NEXT_PUBLIC_API_URL || ''

function getToken(): string | null {
  try {
    const ls = typeof localStorage !== 'undefined' ? localStorage.getItem('ktp_c_token') : null
    if (ls) return ls
    if (typeof document !== 'undefined') {
      const v = document.cookie.split('; ').find(x => x.startsWith('ktp_c_token='))?.split('=')[1]
      return v || null
    }
    return null
  } catch { return null }
}

export function authHeaders(): HeadersInit {
  const token = getToken()
  return token ? { Authorization: `Bearer ${token}` } : {}
}

export const api = {
  async login(email: string, password: string): Promise<LoginResponse> {
    const url = `${API_BASE}/api/auth/login`
    const res = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, password }) })
    if (!res.ok) throw new Error('Giriş başarısız')
    return res.json()
  }
}

