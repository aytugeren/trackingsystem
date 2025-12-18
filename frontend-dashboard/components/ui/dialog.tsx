"use client"
import { ReactNode, useEffect, useRef } from 'react'
import ReactDOM from 'react-dom'
import { cn } from './cn'

type DialogProps = { open: boolean; onOpenChange?: (open: boolean) => void; children: ReactNode }

export function Dialog({ open, onOpenChange, children }: DialogProps) {
  if (!open) return null
  return ReactDOM.createPortal(
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4"
      role="dialog"
      aria-modal="true"
      onClick={() => onOpenChange?.(false)}
    >
      <div className="absolute inset-0" />
      <div onClick={(e) => e.stopPropagation()} className="relative max-h-[90vh] w-full max-w-lg overflow-auto">
        {children}
      </div>
    </div>,
    document.body
  )
}

type DialogContentProps = { children: ReactNode; className?: string }
export function DialogContent({ children, className }: DialogContentProps) {
  return (
    <div className={cn('rounded-lg border bg-background p-4 shadow-lg', className)}>
      {children}
    </div>
  )
}

type DialogHeaderProps = { children: ReactNode }
export function DialogHeader({ children }: DialogHeaderProps) {
  return <div className="mb-3">{children}</div>
}

type DialogTitleProps = { children: ReactNode; className?: string }
export function DialogTitle({ children, className }: DialogTitleProps) {
  return <h2 className={cn('text-lg font-semibold', className)}>{children}</h2>
}
