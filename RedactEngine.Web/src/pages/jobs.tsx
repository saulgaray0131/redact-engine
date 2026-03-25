import { Link } from 'react-router-dom'
import { Plus, FileImage, FileVideo, Clock, CheckCircle, XCircle, Loader2 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'
import { useRedactJobs } from '@/hooks/use-redact-jobs'
import type { RedactJob, JobStatus } from '@/types'

const mockJobs: RedactJob[] = [
  {
    id: '1',
    fileName: 'photo-001.jpg',
    fileUrl: '#',
    fileType: 'image',
    prompt: 'Blur all faces in the image',
    targetTypes: ['face'],
    status: 'completed',
    result: { outputUrl: '#', redactedCount: 3, processingTimeMs: 1200 },
    createdAt: '2026-03-20T10:00:00Z',
    updatedAt: '2026-03-20T10:01:00Z',
  },
  {
    id: '2',
    fileName: 'dashcam-clip.mp4',
    fileUrl: '#',
    fileType: 'video',
    prompt: 'Redact all license plates',
    targetTypes: ['license-plate'],
    status: 'processing',
    result: null,
    createdAt: '2026-03-21T14:30:00Z',
    updatedAt: '2026-03-21T14:30:00Z',
  },
  {
    id: '3',
    fileName: 'id-scan.png',
    fileUrl: '#',
    fileType: 'image',
    prompt: 'Remove all text and personal information',
    targetTypes: ['text'],
    status: 'pending',
    result: null,
    createdAt: '2026-03-22T09:15:00Z',
    updatedAt: '2026-03-22T09:15:00Z',
  },
  {
    id: '4',
    fileName: 'meeting-recording.mp4',
    fileUrl: '#',
    fileType: 'video',
    prompt: 'Blur faces of non-consenting participants',
    targetTypes: ['face', 'custom'],
    status: 'failed',
    result: null,
    createdAt: '2026-03-23T16:45:00Z',
    updatedAt: '2026-03-23T16:50:00Z',
  },
]

const statusConfig: Record<JobStatus, { icon: typeof Clock; label: string; variant: 'default' | 'secondary' | 'destructive'; className?: string }> = {
  pending: { icon: Clock, label: 'Pending', variant: 'secondary' },
  processing: { icon: Loader2, label: 'Processing', variant: 'default' },
  completed: { icon: CheckCircle, label: 'Completed', variant: 'default', className: 'bg-green-600/10 text-green-700 dark:bg-green-500/20 dark:text-green-400' },
  failed: { icon: XCircle, label: 'Failed', variant: 'destructive' },
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

export default function JobsPage() {
  const { data } = useRedactJobs()
  const jobs = data ?? mockJobs

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold tracking-tight">Redact Jobs</h1>
        <Button render={<Link to="/app/new" />}>
          <Plus className="size-4" />
          New Job
        </Button>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {jobs.map((job) => {
          const status = statusConfig[job.status]
          const StatusIcon = status.icon

          return (
            <Link key={job.id} to={`/app/jobs/${job.id}`} className="group">
              <Card className="transition-shadow group-hover:ring-2 group-hover:ring-ring/30">
                <CardHeader>
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      {job.fileType === 'video' ? (
                        <FileVideo className="size-4 text-muted-foreground" />
                      ) : (
                        <FileImage className="size-4 text-muted-foreground" />
                      )}
                      <CardTitle className="truncate">{job.fileName}</CardTitle>
                    </div>
                    <Badge
                      variant={status.variant}
                      className={cn(status.className)}
                    >
                      <StatusIcon className={cn('size-3', job.status === 'processing' && 'animate-spin')} />
                      {status.label}
                    </Badge>
                  </div>
                </CardHeader>
                <CardContent className="flex flex-col gap-2">
                  <p className="line-clamp-2 text-sm text-muted-foreground">
                    {job.prompt}
                  </p>
                  <p className="text-xs text-muted-foreground">
                    {formatDate(job.createdAt)}
                  </p>
                </CardContent>
              </Card>
            </Link>
          )
        })}
      </div>
    </div>
  )
}
