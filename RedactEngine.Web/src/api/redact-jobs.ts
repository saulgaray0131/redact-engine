import type { RedactJob, CreateRedactJobRequest, UploadAssetResponse } from '@/types';
import { apiClient } from './client';

export async function uploadAsset(file: File): Promise<UploadAssetResponse> {
  const formData = new FormData();
  formData.append('file', file);
  return apiClient.postForm<UploadAssetResponse>('/api/assets/upload', formData);
}

export async function createRedactJob(request: CreateRedactJobRequest): Promise<RedactJob> {
  const formData = new FormData();
  formData.append('file', request.file);
  formData.append('prompt', request.prompt);
  return apiClient.postForm<RedactJob>('/api/redact-jobs', formData);
}

export async function listRedactJobs(): Promise<RedactJob[]> {
  return apiClient.get<RedactJob[]>('/api/redact-jobs');
}

export async function getRedactJob(id: string): Promise<RedactJob> {
  return apiClient.get<RedactJob>(`/api/redact-jobs/${encodeURIComponent(id)}`);
}

export async function deleteRedactJob(id: string): Promise<void> {
  return apiClient.delete(`/api/redact-jobs/${encodeURIComponent(id)}`);
}
