import React from 'react'
import ReactDOM from 'react-dom/client'
import * as Sentry from '@sentry/react'
import App from './App'
import './index.css'

// Error tracking — enabled only when a DSN is provided (VITE_SENTRY_DSN). No DSN = no reporting,
// so local dev needs no setup; the error boundary below still catches crashes either way.
const dsn = import.meta.env.VITE_SENTRY_DSN
if (dsn) {
  Sentry.init({
    dsn,
    environment: import.meta.env.MODE,
    tracesSampleRate: 0.2,
  })
}

function Fallback() {
  return (
    <div style={{ display: 'grid', placeItems: 'center', minHeight: '100vh', padding: '1rem', textAlign: 'center' }}>
      <div>
        <h1 style={{ fontSize: 18, fontWeight: 600, margin: 0 }}>Something went wrong</h1>
        <p style={{ opacity: 0.7, marginTop: 8 }}>The error has been reported. Please reload the page.</p>
      </div>
    </div>
  )
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <Sentry.ErrorBoundary fallback={<Fallback />}>
      <App />
    </Sentry.ErrorBoundary>
  </React.StrictMode>,
)
