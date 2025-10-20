import Link from 'next/link'
import LogoutButton from '../components/LogoutButton'

export default function Page() {
  return (
    <main>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h1>Kasiyer Uygulamas��</h1>
        <LogoutButton />
      </div>
      <div className="link-grid">
        <Link href="/invoice/new">
          <button className="primary" style={{ width: '100%' }}>Yeni İşlem</button>
        </Link>
        <Link href="/pricing/display">
          <button className="secondary" style={{ width: '100%' }}>Alt��n Fiyat��</button>
        </Link>
        <Link href="/pricing/settings">
          <button className="secondary" style={{ width: '100%' }}>Fiyat Ayarlar��</button>
        </Link>
      </div>
    </main>
  )
}