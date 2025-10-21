import { cn } from './cn'

export function Table({ className, ...props }: React.HTMLAttributes<HTMLTableElement>) {
  return (
    <div className="w-full overflow-x-auto">
      <table className={cn('w-full caption-bottom text-sm', className)} {...props} />
    </div>
  )
}

export function THead(props: React.HTMLAttributes<HTMLTableSectionElement>) {
  return <thead {...props} />
}
export function TBody(props: React.HTMLAttributes<HTMLTableSectionElement>) {
  return <tbody {...props} />
}
export function TR({ className, ...props }: React.HTMLAttributes<HTMLTableRowElement>) {
  return <tr className={cn('border-b hover:bg-muted/40', className)} {...props} />
}
export function TH(props: React.ThHTMLAttributes<HTMLTableCellElement>) {
  return <th className="h-10 px-3 text-left align-middle font-medium sticky top-0 bg-background z-[1]" {...props} />
}
export function TD(props: React.TdHTMLAttributes<HTMLTableCellElement>) {
  return <td className="p-3 align-middle" {...props} />
}
