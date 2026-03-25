import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { listRedactJobs, getRedactJob, createRedactJob } from '@/api'
import type { CreateRedactJobRequest } from '@/types'

export function useRedactJobs() {
  return useQuery({
    queryKey: ['redact-jobs'],
    queryFn: listRedactJobs,
  })
}

export function useRedactJob(id: string) {
  return useQuery({
    queryKey: ['redact-jobs', id],
    queryFn: () => getRedactJob(id),
    enabled: !!id,
  })
}

export function useCreateRedactJob() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: CreateRedactJobRequest) => createRedactJob(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['redact-jobs'] })
    },
  })
}
