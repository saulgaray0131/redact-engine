import { createBrowserRouter } from 'react-router-dom'
import { Suspense } from 'react'
import { AppLayout, HomePage, NewJobPage, JobsPage, JobDetailPage, SettingsPage } from './lazy-pages'

// Inline wrapper to avoid exporting a component alongside the router constant
const wrap = (Component: React.ComponentType) => (
  <Suspense fallback={<div className="flex h-screen items-center justify-center">Loading...</div>}>
    <Component />
  </Suspense>
)

export const router = createBrowserRouter([
  {
    path: '/',
    element: wrap(HomePage),
  },
  {
    path: '/app',
    element: wrap(AppLayout),
    children: [
      {
        path: 'jobs',
        element: wrap(JobsPage),
      },
      {
        path: 'jobs/:id',
        element: wrap(JobDetailPage),
      },
      {
        path: 'new',
        element: wrap(NewJobPage),
      },
    ],
  },
  {
    path: '/settings',
    element: wrap(AppLayout),
    children: [
      {
        index: true,
        element: wrap(SettingsPage),
      },
    ],
  },
])
