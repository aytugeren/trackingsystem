"use client"
import React from 'react'

export default function SuccessToast({ message }: { message: string }) {
  if (!message) return null
  return <div className="toast success">{message}</div>
}

