# Depicta — Frontend (Phase 3)

React + TypeScript + Tailwind review UI for the human-in-the-loop alt-text workflow:
scan → side-by-side review → inline edit (125-char WCAG limit) → bulk approve / reject →
1-click revert. Styled to the design manifesto (Vercel/Linear/Stripe feel: zinc neutrals,
a single emerald accent, Inter, tabular numerals, fast 150ms transitions — no purple,
no glassmorphism).

## Run

```bash
npm install
npm run dev       # http://localhost:5173
npm run build     # type-check + production build
npm run preview   # serve the production build on :4173
```

## Where the data comes from

The UI talks to an `ApiClient` interface (`src/api/client.ts`). Two implementations satisfy it:
`src/api/mockClient.ts` (in-memory, seeded, simulated latency — the default when `VITE_API_BASE`
is unset) and `src/api/httpClient.ts` (fetch against the real .NET backend, selected when you set
`VITE_API_BASE`). No component code differs between them.

### Sign in

The app opens on a **login screen** and holds the JWT from `POST /api/auth/login` in memory
(never in `localStorage`, to keep it out of reach of XSS), attaching it as `Authorization: Bearer`
to every call. A 401 from any request drops back to the login screen. Against the mock, the
dev credentials `admin` / `depicta-dev-2026` are accepted; against the backend, whatever you
configured there.

## Structure

```
src/
├── api/            types.ts · client.ts (interface) · mockClient.ts
├── components/
│   ├── ui/         Button · StatusBadge · Checkbox · Toast   (custom, no component lib)
│   └── MediaReviewRow.tsx
├── lib/cn.ts
├── App.tsx         scan bar · tabs+counts · bulk toolbar · list · toasts
└── index.css       Tailwind + tokens
```

## Verified

Built clean (`tsc -b && vite build`) and driven end-to-end in a headless browser: select-all
→ bulk approve moves items to **Applied**, counts update, the confirmation toast fires, and
the empty state renders. No runtime/console errors (bar the Google-Fonts fetch, which is
blocked only in the CI sandbox).
