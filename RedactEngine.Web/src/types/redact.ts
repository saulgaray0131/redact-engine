export type JobStatus =
  | 'Pending'
  | 'Detecting'
  | 'AwaitingReview'
  | 'Redacting'
  | 'Completed'
  | 'Failed'
  | 'Cancelled'

export type RedactionStyle = 'Blur' | 'Pixelate' | 'Fill'

export interface RedactJob {
  id: string
  prompt: string
  redactionStyle: RedactionStyle
  confidenceThreshold: number
  originalVideoUrl: string
  originalFileName: string
  redactedVideoUrl: string | null
  status: JobStatus
  errorMessage: string | null
  detectionPreviewUrl: string | null
  totalProcessingTimeMs: number | null
  objectsDetected: number | null
  framesProcessed: number | null
  createdAt: string
  updatedAt: string
}

export interface SubmitJobResponse {
  jobId: string
  status: string
}

export interface CreateRedactJobRequest {
  file: File
  prompt: string
  redactionStyle: RedactionStyle
  confidenceThreshold: number
}
