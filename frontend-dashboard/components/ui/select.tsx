import * as React from 'react'
import { cn } from './cn'

export interface SelectProps extends React.SelectHTMLAttributes<HTMLSelectElement> {}

export function Select({ className, children, ...props }: SelectProps) {
  return (
    <select
      className={cn(
        'h-10 w-full rounded-md border border-input bg-background px-3 pr-8 text-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
        className
      )}
      {...props}
    >
      {children}
    </select>
  )
}

