import { Link } from 'react-router-dom';
import {
  Plus,
  FileVideo,
  Clock,
  CheckCircle,
  XCircle,
  Loader2,
  Trash2,
  Search,
  Ban,
  Play,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';
import { useRedactJobs, useDeleteRedactJob } from '@/hooks/use-redact-jobs';
import type { JobStatus } from '@/types';

const statusConfig: Record<
  JobStatus,
  {
    icon: typeof Clock;
    label: string;
    variant: 'default' | 'secondary' | 'destructive';
    className?: string;
    spin?: boolean;
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
};

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export default function JobsPage() {
  const { data: jobs, isLoading } = useRedactJobs();
  const deleteJobMutation = useDeleteRedactJob();

  const handleDelete = async (jobId: string) => {
    if (confirm('Are you sure you want to delete this job? This action cannot be undone.')) {
      try {
        await deleteJobMutation.mutateAsync(jobId);
      } catch (error) {
        alert(
          `Failed to delete job: ${error instanceof Error ? error.message : 'Unknown error'}. Please try again.`,
        );
      }
    }
  };

  return (
    <div className='flex flex-col gap-6'>
      <div className='flex items-center justify-between'>
        <h1 className='text-2xl font-bold tracking-tight'>Redact Jobs</h1>
        <Button render={<Link to='/app/new' />}>
          <Plus className='size-4' />
          New Job
        </Button>
      </div>

      {isLoading ? (
        <div className="flex items-center justify-center py-20">
          <Loader2 className="size-6 animate-spin text-muted-foreground" />
        </div>
      ) : !jobs || jobs.length === 0 ? (
        <div className="flex flex-col items-center gap-4 py-20 text-center">
          <FileVideo className="size-12 text-muted-foreground/50" />
          <div>
            <p className="text-lg font-medium">No jobs yet</p>
            <p className="text-sm text-muted-foreground">Upload a video and describe what to redact to get started.</p>
          </div>
          <Button render={<Link to='/app/new' />}>
            <Plus className='size-4' />
            Create First Job
          </Button>
        </div>
      ) : (
        <div className='grid gap-4 sm:grid-cols-2 lg:grid-cols-3'>
          {jobs.map((job) => {
            const status = statusConfig[job.status];
            const StatusIcon = status.icon;

            return (
              <Link key={job.id} to={`/app/jobs/${job.id}`} className='group'>
                <Card className='transition-shadow group-hover:ring-2 group-hover:ring-ring/30'>
                  <CardHeader>
                    <div className='flex items-center justify-between'>
                      <div className='flex items-center gap-2'>
                        <FileVideo className='size-4 text-muted-foreground' />
                        <CardTitle className='truncate'>{job.originalFileName}</CardTitle>
                      </div>
                      <div className='flex items-center gap-2'>
                        <Badge variant={status.variant} className={cn(status.className)}>
                          <StatusIcon
                            className={cn('size-3', status.spin && 'animate-spin')}
                          />
                          {status.label}
                        </Badge>
                        <Button
                          variant='ghost'
                          size='sm'
                          onClick={(e) => {
                            e.preventDefault();
                            handleDelete(job.id);
                          }}
                          disabled={deleteJobMutation.isPending}
                        >
                          <Trash2 className='size-4' />
                        </Button>
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent className='flex flex-col gap-2'>
                    <p className='line-clamp-2 text-sm text-muted-foreground'>{job.prompt}</p>
                    <p className='text-xs text-muted-foreground'>{formatDate(job.createdAt)}</p>
                  </CardContent>
                </Card>
              </Link>
            );
          })}
        </div>
      )}
    </div>
  );
}
