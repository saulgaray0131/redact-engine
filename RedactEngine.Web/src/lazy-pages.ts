import { lazy } from 'react'

export const AppLayout = lazy(() => import('@/components/layout/app-layout'))
export const HomePage = lazy(() => import('@/pages/home'))
export const NewJobPage = lazy(() => import('@/pages/new-job'))
export const JobsPage = lazy(() => import('@/pages/jobs'))
export const JobDetailPage = lazy(() => import('@/pages/job-detail'))
export const SettingsPage = lazy(() => import('@/pages/settings'))
