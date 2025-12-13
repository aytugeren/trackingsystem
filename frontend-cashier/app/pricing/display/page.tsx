"use client"
import { useEffect } from 'react'
import { useRouter } from 'next/navigation'

export default function PricingDisplayRedirect() {
  const router = useRouter()
  useEffect(() => { router.replace('/pricing/gold') }, [router])
  return null
}
