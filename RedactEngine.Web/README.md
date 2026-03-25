# RedactEngine.Web

Frontend for RedactEngine — a tool for uploading images/videos and redacting content using natural-language prompts.

## Tech Stack

- React 19 + TypeScript (strict)
- Vite
- Tailwind CSS
- shadcn/ui
- Lucide React (icons)
- React Router
- TanStack Query

## Getting Started

### Prerequisites

- Node.js 20+
- pnpm

### Install

```bash
pnpm install
```

### Environment

Copy `.env.example` to `.env` and configure:

```bash
cp .env.example .env
```

| Variable | Description | Default |
|---|---|---|
| `VITE_API_BASE_URL` | Backend API base URL | `http://localhost:4000` |

### Development

```bash
pnpm dev
```

### Build

```bash
pnpm build
```

### Preview

```bash
pnpm preview
```

## Architecture

```
src/
├── api/          # API client and endpoint modules
├── components/
│   ├── layout/   # App shell, nav, shared layout
│   └── ui/       # shadcn/ui components
├── config/       # App configuration
├── hooks/        # Custom React hooks
├── lib/          # Utility functions
├── pages/        # Route pages
├── providers/    # React context providers
└── types/        # TypeScript interfaces and types
```

### Key Patterns

- **Feature-first organization** — code is grouped by domain
- **Provider-based composition** — ThemeProvider -> AuthProvider -> QueryProvider -> Router
- **Centralized API client** — single fetch wrapper with auth token injection support
- **TanStack Query** — server state management, separate from UI state

### Auth

Auth is scaffolded but **disabled by default** (`authEnabled: false` in app config). The `AuthProvider` and `useAuth()` hook are ready for Auth0 or custom JWT integration.

### Backend Integration

API client modules in `src/api/` contain typed placeholder methods. Connect them to the real backend by updating `VITE_API_BASE_URL` and implementing the fetch calls.
