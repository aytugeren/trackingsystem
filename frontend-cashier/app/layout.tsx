import './globals.css'
import GlobalKaratAlert from '../components/GlobalKaratAlert'
import LoginSuccessAlert from '../components/LoginSuccessAlert'

export const metadata = {
  title: 'Kuyumculuk - Kasiyer',
  description: 'Kasiyer uygulaması'
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="tr">
      <head>
        <meta charSet="utf-8" />
        <meta httpEquiv="Content-Type" content="text/html; charset=utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
      </head>
      <body>
        <GlobalKaratAlert />
        <LoginSuccessAlert />
        {children}
      </body>
    </html>
  )
}
