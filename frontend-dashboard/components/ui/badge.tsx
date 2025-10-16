import { cn } from './cn'

export function Badge({ children, variant = 'default', className }: { children: React.ReactNode; variant?: 'default' | 'outline' | 'success' | 'warning' | 'destructive'; className?: string }) {
  const styles = {
    default: 'bg-secondary text-secondary-foreground',
    outline: 'border border-input text-foreground',
    success: 'bg-emerald-100 text-emerald-800',
    warning: 'bg-amber-100 text-amber-800',
    destructive: 'bg-red-100 text-red-800',
  } as const
  return (
    <span className={cn('inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium', styles[variant], className)}>
      {children}
    </span>
  )
}

