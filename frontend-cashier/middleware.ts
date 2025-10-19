import type { NextRequest } from 'next/server'
import { NextResponse } from 'next/server'

export function middleware(req: NextRequest) {
  const token = req.cookies.get('ktp_c_token')?.value
  const { pathname, origin } = req.nextUrl

  const isLogin = pathname.startsWith('/login')
  const isApiAuth = pathname.startsWith('/api/auth/login')
  const isPublic = isLogin || isApiAuth || pathname.startsWith('/_next') || pathname === '/favicon.ico'

  if (!token && !isPublic) {
    const url = new URL('/login', origin)
    return NextResponse.redirect(url)
  }

  if (token && isLogin) {
    const url = new URL('/', origin)
    return NextResponse.redirect(url)
  }

  return NextResponse.next()
}

export const config = {
  matcher: ['/((?!_next/static|_next/image|favicon.ico).*)'],
}
