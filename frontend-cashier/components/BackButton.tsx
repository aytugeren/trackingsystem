"use client"
import { useRouter } from 'next/navigation'

export default function BackButton({ label = 'Geri Dön' }: { label?: string }) {
  const router = useRouter()
  return (
    <button
      type="button"
      className="secondary"
      onClick={() => router.back()}
      style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}
    >
      <span style={{ fontSize: 20, lineHeight: 1 }}>←</span>
      {label}
    </button>
  )
}

