import Link from 'next/link'
import LogoutButton from '../components/LogoutButton'

export default function Page() {
  return (
    <main>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h1>Kasiyer Uygulaması</h1>
        <LogoutButton />
      </div>
      <div className="link-grid">
        <Link href="/invoice/new">
          <button className="primary" style={{ width: '100%' }}>Yeni İşlem</button>
        </Link>
        <Link href="/pricing/display">
          <button className="secondary" style={{ width: '100%' }}>Altın Fiyatı</button>
        </Link>
        <Link href="/pricing/settings">
          <button className="secondary" style={{ width: '100%' }}>Fiyat Ayarları</button>
        </Link>
      </div>
    </main>
  )
}

