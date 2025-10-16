"use client"
import React from 'react'

export default function ErrorToast({ message }: { message: string }) {
  if (!message) return null
  return <div className="toast error">{message}</div>
}

