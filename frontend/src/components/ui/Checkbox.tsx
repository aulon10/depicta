import { Check, Minus } from 'lucide-react'
import { cn } from '../../lib/cn'

interface Props {
  checked: boolean
  indeterminate?: boolean
  onChange: (checked: boolean) => void
  'aria-label'?: string
}

export function Checkbox({ checked, indeterminate, onChange, ...rest }: Props) {
  const active = checked || indeterminate
  return (
    <button
      type="button"
      role="checkbox"
      aria-checked={indeterminate ? 'mixed' : checked}
      onClick={() => onChange(!checked)}
      className={cn(
        'grid h-[18px] w-[18px] place-items-center rounded-[5px] border transition-colors duration-150 ease-out',
        active ? 'bg-brand-700 border-brand-700 text-white' : 'bg-surface border-line-strong hover:border-fg-subtle',
      )}
      {...rest}
    >
      {indeterminate ? <Minus size={12} strokeWidth={3} /> : checked ? <Check size={12} strokeWidth={3} /> : null}
    </button>
  )
}
