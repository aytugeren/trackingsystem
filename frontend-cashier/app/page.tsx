import Link from 'next/link'

export default function Page() {
  return (
    <main>
      <h1>Kasiyer Uygulaması</h1>
      <div className="link-grid">
        <Link href="/invoice/new">
          <button className="primary" style={{ width: '100%' }}>Yeni Fatura</button>
        </Link>
        <Link href="/expense/new">
          <button className="secondary" style={{ width: '100%' }}>Yeni Gider</button>
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
