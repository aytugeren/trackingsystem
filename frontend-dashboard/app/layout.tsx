export const metadata = {
  title: 'Kuyumculuk - Yönetim',
  description: 'Yönetim paneli'
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="tr">
      <body>
        {children}
      </body>
    </html>
  )
}

