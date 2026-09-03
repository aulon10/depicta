import { cn } from '../lib/cn'

/**
 * Depicta brand mark: an emerald-gradient tile carrying a minimal landscape (the image)
 * lit by an AI sparkle (the generation) — "describe what's in the picture". Paired with the
 * Bricolage Grotesque wordmark for a distinctive, non-generic identity.
 */
export function Logo({
  size = 'md',
  showWordmark = true,
  className,
}: {
  size?: 'sm' | 'md' | 'lg'
  showWordmark?: boolean
  className?: string
}) {
  const tile = size === 'lg' ? 40 : size === 'sm' ? 28 : 34
  const text = size === 'lg' ? 'text-[26px]' : size === 'sm' ? 'text-[17px]' : 'text-[20px]'

  return (
    <span className={cn('inline-flex items-center gap-2.5', className)}>
      <LogoMark px={tile} />
      {showWordmark && (
        <span className={cn('font-display font-bold tracking-[-0.03em] text-zinc-900', text)}>
          Depicta
        </span>
      )}
    </span>
  )
}

export function LogoMark({ px = 34 }: { px?: number }) {
  return (
    <svg width={px} height={px} viewBox="0 0 40 40" fill="none" role="img" aria-label="Depicta" className="shrink-0">
      <defs>
        <linearGradient id="depicta-grad" x1="4" y1="2" x2="36" y2="38" gradientUnits="userSpaceOnUse">
          <stop stopColor="#18d29a" />
          <stop offset="1" stopColor="#0a5f45" />
        </linearGradient>
      </defs>
      <rect width="40" height="40" rx="11" fill="url(#depicta-grad)" />
      {/* subtle top sheen */}
      <rect width="40" height="20" rx="11" fill="#ffffff" fillOpacity="0.06" />
      {/* landscape — the image */}
      <path d="M3.5 29.6 L13 18 L22.5 29.6 Z" fill="#ffffff" fillOpacity="0.9" />
      <path d="M15 29.6 L24 19.4 L33.5 29.6 Z" fill="#ffffff" />
      {/* AI sparkle — the generated description */}
      <path d="M28 9 Q28 13.5 32.5 13.5 Q28 13.5 28 18 Q28 13.5 23.5 13.5 Q28 13.5 28 9 Z" fill="#ffffff" />
    </svg>
  )
}
