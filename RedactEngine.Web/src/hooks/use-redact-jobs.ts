import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { listRedactJobs, getRedactJob, createRedactJob, confirmRedactJob, cancelRedactJob, deleteRedactJob } from '@/api/redact-jobs';
import type { CreateRedactJobRequest, JobStatus } from '@/types';

const IN_PROGRESS_STATUSES: JobStatus[] = ['Pending', 'Detecting', 'Redacting'];

export function useRedactJobs() {
  return useQuery({
    queryKey: ['redact-jobs'],
    queryFn: listRedactJobs,
  });
}

export function useRedactJob(id: string) {
  const query = useQuery({
    queryKey: ['redact-jobs', id],
    queryFn: () => getRedactJob(id),
    enabled: !!id,
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      if (status && IN_PROGRESS_STATUSES.includes(status)) {
        return 3000;
      }
      return false;
    },
  });
  return query;
}

export function useCreateRedactJob() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateRedactJobRequest) => createRedactJob(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['redact-jobs'] });
    },
  });
}

export function useConfirmRedactJob() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => confirmRedactJob(id),
    onSuccess: (_data, id) => {
      queryClient.invalidateQueries({ queryKey: ['redact-jobs', id] });
      queryClient.invalidateQueries({ queryKey: ['redact-jobs'] });
    },
  });
}

export function useCancelRedactJob() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => cancelRedactJob(id),
    onSuccess: (_data, id) => {
      queryClient.invalidateQueries({ queryKey: ['redact-jobs', id] });
      queryClient.invalidateQueries({ queryKey: ['redact-jobs'] });
    },
  });
}

export function useDeleteRedactJob() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => deleteRedactJob(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['redact-jobs'] });
    },
  });
}
