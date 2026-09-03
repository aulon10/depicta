import { Check, X, RotateCcw, Undo2, Sparkles, Loader2, AlertTriangle } from 'lucide-react'
import type { MediaItem } from '../api/types'
import { Button } from './ui/Button'
import { StatusBadge } from './ui/StatusBadge'
import { Checkbox } from './ui/Checkbox'
import { cn } from '../lib/cn'

const MAX = 125

interface Props {
  item: MediaItem
  selected: boolean
  editValue: string
  onToggleSelect: (id: string) => void
  onEdit: (id: string, value: string) => void
  onApprove: (id: string) => void
  onReject: (id: string) => void
  onRegenerate: (id: string) => void
  onRevert: (id: string) => void
}

export function MediaReviewRow(p: Props) {
  const { item } = p
  const proposal = item.activeProposal
  const value = p.editValue
  const count = value.length
  const over = count > MAX
  const edited = proposal ? value.trim() !== proposal.proposedAlt.trim() : false

  const actions = (
    <>
      {item.state === 'pending_review' && (
        <>
          <Button size="sm" variant="primary" disabled={over || value.trim().length === 0} onClick={() => p.onApprove(item.id)}>
            <Check size={13} /> Approve
          </Button>
          <Button size="sm" variant="ghost" onClick={() => p.onReject(item.id)}>
            <X size={13} /> Reject
          </Button>
          <Button size="sm" variant="ghost" onClick={() => p.onRegenerate(item.id)}>
            <RotateCcw size={13} /> Redo
          </Button>
        </>
      )}
      {item.state === 'applied' && (
        <Button size="sm" variant="secondary" onClick={() => p.onRevert(item.id)}>
          <Undo2 size={13} /> Revert
        </Button>
      )}
      {item.state === 'reverted' && (
        <Button size="sm" variant="primary" onClick={() => p.onApprove(item.id)}>
          <Check size={13} /> Re-apply
        </Button>
      )}
    </>
  )

  return (
    <div className={cn(
      'flex gap-3 border-b border-zinc-200 px-4 py-4 transition-colors duration-150 sm:gap-4',
      p.selected ? 'bg-brand-50/40' : 'bg-white hover:bg-zinc-50/60',
    )}>
      <div className="pt-1">
        <Checkbox
          checked={p.selected}
          onChange={() => p.onToggleSelect(item.id)}
          aria-label={`Select ${item.filename}`}
        />
      </div>

      {/* Original image */}
      <div className="shrink-0">
        <img
          src={item.url}
          alt=""
          className="h-[76px] w-[104px] rounded-lg border border-zinc-200 object-cover sm:h-[99px] sm:w-[132px]"
        />
        <div className="mt-1.5 max-w-[104px] truncate text-xs text-zinc-500 sm:max-w-[132px]" title={item.filename}>
          {item.filename}
        </div>
        <div className="tnum text-[11px] text-zinc-400">
          #{item.wpAttachmentId}{item.width ? ` · ${item.width}×${item.height}` : ''}
        </div>
      </div>

      {/* Proposal / state */}
      <div className="min-w-0 flex-1">
        <div className="mb-2 flex flex-wrap items-center gap-x-2 gap-y-1">
          <StatusBadge state={item.state} />
          {proposal && (
            <span className="inline-flex items-center gap-1 text-xs text-zinc-400">
              <Sparkles size={12} className="text-brand-700" />
              {proposal.model}
              {proposal.confidence != null && (
                <span className="tnum">· {Math.round(proposal.confidence * 100)}%</span>
              )}
            </span>
          )}
        </div>

        {item.state === 'processing' && (
          <div className="flex items-center gap-2 py-3 text-sm text-zinc-500">
            <Loader2 size={15} className="animate-spin" /> Generating a description…
          </div>
        )}

        {item.state === 'failed' && (
          <div className="flex flex-col gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2.5 sm:flex-row sm:items-center sm:justify-between">
            <span className="flex items-center gap-2 text-sm text-red-700">
              <AlertTriangle size={15} className="shrink-0" /> {item.lastError ?? 'Something went wrong'}
            </span>
            <Button size="sm" variant="secondary" className="self-start" onClick={() => p.onRegenerate(item.id)}>
              <RotateCcw size={13} /> Retry
            </Button>
          </div>
        )}

        {proposal && item.state !== 'processing' && (
          <>
            <label className="sr-only" htmlFor={`alt-${item.id}`}>Alt text</label>
            <textarea
              id={`alt-${item.id}`}
              value={value}
              onChange={(e) => p.onEdit(item.id, e.target.value)}
              rows={2}
              disabled={item.state === 'rejected'}
              className={cn(
                'w-full resize-none rounded-lg border bg-white px-3 py-2 text-[15px] leading-snug text-zinc-800',
                'transition-colors duration-150 placeholder:text-zinc-400 focus:outline-none',
                over ? 'border-red-300 focus:border-red-400' : 'border-zinc-200 focus:border-brand-600',
                item.state === 'rejected' && 'opacity-50',
              )}
            />
            <div className="mt-1.5 flex items-center justify-between gap-3 text-xs">
              <span className="min-w-0 truncate text-zinc-400">
                {item.currentAlt
                  ? <>was: <span className="text-zinc-500">“{item.currentAlt}”</span></>
                  : <span className="text-zinc-400">was empty · {edited ? 'edited' : 'AI suggestion'}</span>}
              </span>
              <span className={cn('tnum shrink-0', over ? 'font-medium text-red-600' : 'text-zinc-400')}>
                {count}/{MAX}
              </span>
            </div>
          </>
        )}

        {/* Mobile actions — below the content */}
        <div className="mt-3 flex flex-wrap gap-2 sm:hidden">{actions}</div>
      </div>

      {/* Desktop actions — right column */}
      <div className="hidden w-[112px] shrink-0 flex-col items-stretch gap-1.5 pt-0.5 sm:flex">
        {actions}
      </div>
    </div>
  )
}
