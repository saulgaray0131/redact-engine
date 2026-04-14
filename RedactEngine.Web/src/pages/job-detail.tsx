import { useParams, Link } from 'react-router-dom'
import {
  ArrowLeft,
  FileVideo,
  Clock,
  CheckCircle,
  XCircle,
  Loader2,
  Eye,
  Search,
  Ban,
  Play,
  Check,
  X,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Separator } from '@/components/ui/separator'
import { cn } from '@/lib/utils'
import { useRedactJob, useConfirmRedactJob, useCancelRedactJob } from '@/hooks/use-redact-jobs'
import type { JobStatus } from '@/types'

const statusConfig: Record<
  JobStatus,
  {
    icon: typeof Clock
    label: string
    variant: 'default' | 'secondary' | 'destructive'
    className?: string
    spin?: boolean
  }
> = {
  Pending: { icon: Clock, label: 'Pending', variant: 'secondary' },
  Detecting: { icon: Search, label: 'Detecting', variant: 'default', spin: true },
  AwaitingReview: {
    icon: Play,
    label: 'Awaiting Review',
    variant: 'default',
    className: 'bg-amber-600/10 text-amber-700 dark:bg-amber-500/20 dark:text-amber-400',
  },
  Redacting: { icon: Loader2, label: 'Redacting', variant: 'default', spin: true },
  Completed: {
    icon: CheckCircle,
    label: 'Completed',
    variant: 'default',
    className: 'bg-green-600/10 text-green-700 dark:bg-green-500/20 dark:text-green-400',
  },
  Failed: { icon: XCircle, label: 'Failed', variant: 'destructive' },
  Cancelled: { icon: Ban, label: 'Cancelled', variant: 'secondary' },
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
  const { data: job, isLoading } = useRedactJob(id)
  const confirmMutation = useConfirmRedactJob()
  const cancelMutation = useCancelRedactJob()

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-20">
        <Loader2 className="size-6 animate-spin text-muted-foreground" />
      </div>
    )
  }

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
              <FileVideo className="size-5 text-muted-foreground" />
              <CardTitle className="text-lg">{job.originalFileName}</CardTitle>
            </div>
            <Badge variant={status.variant} className={cn(status.className)}>
              <StatusIcon className={cn('size-3', status.spin && 'animate-spin')} />
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
            <h3 className="text-sm font-medium">Settings</h3>
            <div className="flex gap-4 text-sm text-muted-foreground">
              <span>Style: {job.redactionStyle}</span>
              <span>Threshold: {job.confidenceThreshold}</span>
            </div>
          </section>

          <Separator />

          <section className="flex flex-col gap-2">
            <h3 className="text-sm font-medium">Status</h3>

            {job.status === 'Pending' && (
              <p className="text-sm text-muted-foreground">
                Waiting to start detection...
              </p>
            )}

            {job.status === 'Detecting' && (
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Loader2 className="size-4 animate-spin" />
                Analyzing video for objects to redact...
              </div>
            )}

            {job.status === 'AwaitingReview' && (
              <div className="flex flex-col gap-3">
                <div className="rounded-lg border bg-muted/50 p-3">
                  <p className="text-sm font-medium">Detection Complete</p>
                  {job.objectsDetected != null && (
                    <p className="text-sm text-muted-foreground">
                      Found {job.objectsDetected} object{job.objectsDetected !== 1 ? 's' : ''} to redact.
                    </p>
                  )}
                </div>
                <div className="flex gap-2">
                  <Button
                    onClick={() => confirmMutation.mutate(id)}
                    disabled={confirmMutation.isPending || cancelMutation.isPending}
                  >
                    {confirmMutation.isPending ? (
                      <Loader2 className="size-4 animate-spin" />
                    ) : (
                      <Check className="size-4" />
                    )}
                    Confirm Redaction
                  </Button>
                  <Button
                    variant="outline"
                    onClick={() => cancelMutation.mutate(id)}
                    disabled={confirmMutation.isPending || cancelMutation.isPending}
                  >
                    {cancelMutation.isPending ? (
                      <Loader2 className="size-4 animate-spin" />
                    ) : (
                      <X className="size-4" />
                    )}
                    Cancel
                  </Button>
                </div>
              </div>
            )}

            {job.status === 'Redacting' && (
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Loader2 className="size-4 animate-spin" />
                Applying redaction to video...
              </div>
            )}

            {job.status === 'Completed' && (
              <div className="flex flex-col gap-2 text-sm text-muted-foreground">
                {job.objectsDetected != null && (
                  <span>Objects redacted: {job.objectsDetected}</span>
                )}
                {job.framesProcessed != null && (
                  <span>Frames processed: {job.framesProcessed}</span>
                )}
                {job.totalProcessingTimeMs != null && (
                  <span>Processing time: {(job.totalProcessingTimeMs / 1000).toFixed(1)}s</span>
                )}
                {job.redactedVideoUrl && (
                  <Button
                    variant="outline"
                    size="sm"
                    className="mt-1 w-fit"
                    render={<a href={job.redactedVideoUrl} target="_blank" rel="noopener noreferrer" />}
                  >
                    <Eye className="size-4" />
                    View Redacted Video
                  </Button>
                )}
              </div>
            )}

            {job.status === 'Failed' && (
              <p className="text-sm text-destructive">
                {job.errorMessage || 'Job failed. Please try again.'}
              </p>
            )}

            {job.status === 'Cancelled' && (
              <p className="text-sm text-muted-foreground">
                This job was cancelled.
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
