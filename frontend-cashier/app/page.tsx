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
        <Link href="/profile">
          <button className="secondary" style={{ width: '100%' }}>Profilim</button>
        </Link>
        <Link href="/leave">
          <button className="secondary" style={{ width: '100%' }}>İzin İste</button>
        </Link>
        <Link href="/pricing/gold">
          <button className="secondary" style={{ width: '100%' }}>Has Altın</button>
        </Link>
        {/* Fiyat Ayarları yönetim ekranına taşındı */}
      </div>
    </main>
  )
}
