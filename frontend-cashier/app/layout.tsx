export const metadata = {
  title: 'Kuyumculuk - Kasiyer',
  description: 'Kasiyer uygulaması'
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

