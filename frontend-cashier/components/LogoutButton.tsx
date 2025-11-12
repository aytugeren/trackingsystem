"use client"
import { useRouter } from 'next/navigation'

export default function LogoutButton() {
  const router = useRouter()
  function logout() {
    try {
      localStorage.removeItem('ktp_c_token')
      localStorage.removeItem('ktp_c_role')
      localStorage.removeItem('ktp_c_email')
      document.cookie = 'ktp_c_token=; Max-Age=0; Path=/'
      try { window.dispatchEvent(new CustomEvent('ktp:auth-changed')) } catch {}
    } finally {
      router.replace('/login')
    }
  }
  return (
    <button onClick={logout} className="secondary" style={{ float: 'right' }}>Çıkış</button>
  )
}
