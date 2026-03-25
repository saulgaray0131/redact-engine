export type JobStatus = 'pending' | 'processing' | 'completed' | 'failed'

export type RedactTargetType = 'face' | 'text' | 'object' | 'license-plate' | 'custom'

export interface RedactResult {
  outputUrl: string
  redactedCount: number
  processingTimeMs: number
}

export interface RedactJob {
  id: string
  fileName: string
  fileUrl: string
  fileType: 'image' | 'video'
  prompt: string
  targetTypes: RedactTargetType[]
  status: JobStatus
  result: RedactResult | null
  createdAt: string
  updatedAt: string
}

export interface CreateRedactJobRequest {
  file: File
  prompt: string
}

export interface UploadAssetResponse {
  fileUrl: string
  fileName: string
  fileType: 'image' | 'video'
}
