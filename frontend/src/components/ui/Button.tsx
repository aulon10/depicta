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
  secondary: 'bg-white text-zinc-800 border border-zinc-200 hover:bg-zinc-50 hover:border-zinc-300',
  ghost: 'text-zinc-600 hover:bg-zinc-100',
  danger: 'bg-white text-red-700 border border-zinc-200 hover:border-red-200 hover:bg-red-50',
}

export function Button({ variant = 'secondary', size = 'md', className, ...rest }: Props) {
  return <button className={cn(base, sizes[size], variants[variant], className)} {...rest} />
}
