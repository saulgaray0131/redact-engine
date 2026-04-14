import type { RedactJob, CreateRedactJobRequest, SubmitJobResponse } from '@/types';
import { apiClient } from './client';

export async function createRedactJob(request: CreateRedactJobRequest): Promise<SubmitJobResponse> {
  const formData = new FormData();
  formData.append('video', request.file);
  formData.append('prompt', request.prompt);
  return apiClient.postForm<SubmitJobResponse>('/api/redaction-jobs', formData);
}

export async function listRedactJobs(): Promise<RedactJob[]> {
  return apiClient.get<RedactJob[]>('/api/redaction-jobs');
}

export async function getRedactJob(id: string): Promise<RedactJob> {
  return apiClient.get<RedactJob>(`/api/redaction-jobs/${encodeURIComponent(id)}`);
}

export async function confirmRedactJob(id: string): Promise<RedactJob> {
  return apiClient.post<RedactJob>(`/api/redaction-jobs/${encodeURIComponent(id)}/confirm`);
}

export async function cancelRedactJob(id: string): Promise<RedactJob> {
  return apiClient.post<RedactJob>(`/api/redaction-jobs/${encodeURIComponent(id)}/cancel`);
}

export async function deleteRedactJob(id: string): Promise<void> {
  return apiClient.delete(`/api/redaction-jobs/${encodeURIComponent(id)}`);
}
