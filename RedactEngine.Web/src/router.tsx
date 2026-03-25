import { createBrowserRouter } from 'react-router-dom'
import { lazy, Suspense } from 'react'

const AppLayout = lazy(() => import('@/components/layout/app-layout'))
const HomePage = lazy(() => import('@/pages/home'))
const NewJobPage = lazy(() => import('@/pages/new-job'))
const JobsPage = lazy(() => import('@/pages/jobs'))
const JobDetailPage = lazy(() => import('@/pages/job-detail'))
const SettingsPage = lazy(() => import('@/pages/settings'))

function SuspenseWrapper({ children }: { children: React.ReactNode }) {
  return (
    <Suspense fallback={<div className="flex h-screen items-center justify-center">Loading...</div>}>
      {children}
    </Suspense>
  )
}

export const router = createBrowserRouter([
  {
    path: '/',
    element: <SuspenseWrapper><HomePage /></SuspenseWrapper>,
  },
  {
    path: '/app',
    element: <SuspenseWrapper><AppLayout /></SuspenseWrapper>,
    children: [
      {
        path: 'jobs',
        element: <SuspenseWrapper><JobsPage /></SuspenseWrapper>,
      },
      {
        path: 'jobs/:id',
        element: <SuspenseWrapper><JobDetailPage /></SuspenseWrapper>,
      },
      {
        path: 'new',
        element: <SuspenseWrapper><NewJobPage /></SuspenseWrapper>,
      },
    ],
  },
  {
    path: '/settings',
    element: <SuspenseWrapper><AppLayout /></SuspenseWrapper>,
    children: [
      {
        index: true,
        element: <SuspenseWrapper><SettingsPage /></SuspenseWrapper>,
      },
    ],
  },
])
