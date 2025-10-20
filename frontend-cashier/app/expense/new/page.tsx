"use client"
import { useEffect } from 'react'
import { useRouter } from 'next/navigation'

export default function ExpenseNewPage() {
  const router = useRouter()
  useEffect(() => {
    router.replace('/invoice/new?type=expense')
  }, [router])
  return null
}