import { useEffect } from 'react'
import { Check, AlertTriangle, X } from 'lucide-react'
import { cn } from '../../lib/cn'

export interface ToastData {
  id: string
  message: string
  tone?: 'success' | 'error'
}

export function ToastStack({ toasts, onDismiss }: { toasts: ToastData[]; onDismiss: (id: string) => void }) {
  return (
    <div className="fixed bottom-5 right-5 z-50 flex flex-col gap-2">
      {toasts.map((t) => (
        <Toast key={t.id} toast={t} onDismiss={onDismiss} />
      ))}
    </div>
  )
}

function Toast({ toast, onDismiss }: { toast: ToastData; onDismiss: (id: string) => void }) {
  useEffect(() => {
    const h = setTimeout(() => onDismiss(toast.id), 3200)
    return () => clearTimeout(h)
  }, [toast.id, onDismiss])

  const error = toast.tone === 'error'
  return (
    <div
      role="status"
      className={cn(
        'flex items-center gap-2.5 rounded-lg border bg-surface px-3.5 py-2.5 text-sm shadow-lg',
        'animate-[slideIn_.15s_cubic-bezier(0.16,1,0.3,1)]',
        error ? 'border-red-200 dark:border-red-500/40' : 'border-line',
      )}
    >
      <span className={cn('grid h-5 w-5 place-items-center rounded-full', error ? 'bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-300' : 'bg-brand-100 text-brand-800 dark:bg-brand-500/20 dark:text-brand-300')}>
        {error ? <AlertTriangle size={13} /> : <Check size={13} />}
      </span>
      <span className="text-fg">{toast.message}</span>
      <button onClick={() => onDismiss(toast.id)} className="ml-1 text-fg-subtle hover:text-fg">
        <X size={14} />
      </button>
    </div>
  )
}
