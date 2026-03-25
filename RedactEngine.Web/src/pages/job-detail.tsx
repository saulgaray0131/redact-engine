import { useParams, Link } from 'react-router-dom'
import {
  ArrowLeft,
  FileImage,
  FileVideo,
  Clock,
  CheckCircle,
  XCircle,
  Loader2,
  Eye,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Separator } from '@/components/ui/separator'
import { cn } from '@/lib/utils'
import { useRedactJob } from '@/hooks/use-redact-jobs'
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
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

export default function JobDetailPage() {
  const { id = '' } = useParams<{ id: string }>()
  const { data: fetchedJob } = useRedactJob(id)

  const job = fetchedJob ?? mockJobs.find((j) => j.id === id)

  if (!job) {
    return (
      <div className="flex flex-col items-center gap-4 py-20">
        <p className="text-muted-foreground">Job not found.</p>
        <Button variant="outline" render={<Link to="/app/jobs" />}>
          <ArrowLeft className="size-4" />
          Back to Jobs
        </Button>
      </div>
    )
  }

  const status = statusConfig[job.status]
  const StatusIcon = status.icon

  return (
    <div className="mx-auto flex max-w-2xl flex-col gap-6">
      <div>
        <Button variant="ghost" size="sm" render={<Link to="/app/jobs" />}>
          <ArrowLeft className="size-4" />
          Back to Jobs
        </Button>
      </div>

      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              {job.fileType === 'video' ? (
                <FileVideo className="size-5 text-muted-foreground" />
              ) : (
                <FileImage className="size-5 text-muted-foreground" />
              )}
              <CardTitle className="text-lg">{job.fileName}</CardTitle>
            </div>
            <Badge variant={status.variant} className={cn(status.className)}>
              <StatusIcon className={cn('size-3', job.status === 'processing' && 'animate-spin')} />
              {status.label}
            </Badge>
          </div>
        </CardHeader>

        <CardContent className="flex flex-col gap-4">
          <section className="flex flex-col gap-1">
            <h3 className="text-sm font-medium">Prompt</h3>
            <p className="text-sm text-muted-foreground">{job.prompt}</p>
          </section>

          <Separator />

          <section className="flex flex-col gap-1">
            <h3 className="text-sm font-medium">File Info</h3>
            <div className="flex gap-4 text-sm text-muted-foreground">
              <span>Type: {job.fileType}</span>
              <span>Targets: {job.targetTypes.join(', ')}</span>
            </div>
          </section>

          <Separator />

          <section className="flex flex-col gap-1">
            <h3 className="text-sm font-medium">Result</h3>
            {job.status === 'completed' && job.result ? (
              <div className="flex flex-col gap-1 text-sm text-muted-foreground">
                <span>Redacted items: {job.result.redactedCount}</span>
                <span>Processing time: {job.result.processingTimeMs}ms</span>
                <Button
                  variant="outline"
                  size="sm"
                  className="mt-1 w-fit"
                  render={<a href={job.result.outputUrl} target="_blank" rel="noopener noreferrer" />}
                >
                  <Eye className="size-4" />
                  View Output
                </Button>
              </div>
            ) : job.status === 'processing' ? (
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Loader2 className="size-4 animate-spin" />
                Processing…
              </div>
            ) : job.status === 'failed' ? (
              <p className="text-sm text-destructive">
                Job failed. Please try again.
              </p>
            ) : (
              <p className="text-sm text-muted-foreground">
                Waiting to be processed.
              </p>
            )}
          </section>

          <Separator />

          <section className="flex gap-6 text-xs text-muted-foreground">
            <span>Created: {formatDate(job.createdAt)}</span>
            <span>Updated: {formatDate(job.updatedAt)}</span>
          </section>
        </CardContent>
      </Card>
    </div>
  )
}
