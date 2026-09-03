import type { MediaItemState } from '../../api/types'
import { cn } from '../../lib/cn'

const map: Record<MediaItemState, { label: string; className: string; dot: string }> = {
  unprocessed:    { label: 'Unprocessed',   className: 'text-zinc-600 bg-zinc-100 border-zinc-200',    dot: 'bg-zinc-400' },
  processing:     { label: 'Processing',    className: 'text-amber-700 bg-amber-50 border-amber-200',  dot: 'bg-amber-500 animate-pulse' },
  pending_review: { label: 'Pending',       className: 'text-sky-700 bg-sky-50 border-sky-200',        dot: 'bg-sky-500' },
  approved:       { label: 'Approved',      className: 'text-brand-800 bg-brand-50 border-brand-200',  dot: 'bg-brand-600' },
  applied:        { label: 'Applied',       className: 'text-brand-800 bg-brand-50 border-brand-200',  dot: 'bg-brand-600' },
  rejected:       { label: 'Rejected',      className: 'text-zinc-500 bg-zinc-100 border-zinc-200',    dot: 'bg-zinc-400' },
  reverted:       { label: 'Reverted',      className: 'text-zinc-600 bg-zinc-100 border-zinc-200',    dot: 'bg-zinc-400' },
  failed:         { label: 'Failed',        className: 'text-red-700 bg-red-50 border-red-200',        dot: 'bg-red-500' },
}

export function StatusBadge({ state }: { state: MediaItemState }) {
  const s = map[state]
  return (
    <span className={cn(
      'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs font-medium',
      s.className,
    )}>
      <span className={cn('h-1.5 w-1.5 rounded-full', s.dot)} />
      {s.label}
    </span>
  )
}
