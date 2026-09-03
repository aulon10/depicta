import { cn } from '../../lib/cn'

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger'

interface Props extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant
  size?: 'sm' | 'md'
}

const base =
  'inline-flex items-center justify-center gap-2 font-medium rounded-lg transition-colors duration-150 ease-out disabled:opacity-40 disabled:pointer-events-none select-none'

const sizes = {
  sm: 'h-8 px-3 text-[13px]',
  md: 'h-9 px-4 text-sm',
}

const variants: Record<Variant, string> = {
  // single intentional accent for key actions
  primary: 'bg-brand-700 text-white hover:bg-brand-800 shadow-sm',
  secondary: 'bg-surface text-fg border border-line hover:bg-surface-2 hover:border-line-strong',
  ghost: 'text-fg-muted hover:bg-surface-2 hover:text-fg',
  danger: 'bg-surface text-red-600 dark:text-red-400 border border-line hover:border-red-300 hover:bg-red-50 dark:hover:border-red-500/40 dark:hover:bg-red-500/10',
}

export function Button({ variant = 'secondary', size = 'md', className, ...rest }: Props) {
  return <button className={cn(base, sizes[size], variants[variant], className)} {...rest} />
}
