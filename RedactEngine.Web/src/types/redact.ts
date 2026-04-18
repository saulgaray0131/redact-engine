export type JobStatus =
  | 'Pending'
  | 'Detecting'
  | 'AwaitingReview'
  | 'Redacting'
  | 'Completed'
  | 'Failed'
  | 'Cancelled'

export type RedactionStyle = 'Blur' | 'Pixelate' | 'Fill'

export interface DetectionPreview {
  frameIndex: number
  timestampMs: number
  url: string
}

export interface RedactJob {
  id: string
  prompt: string
  detectionPrompt: string
  redactionStyle: RedactionStyle
  confidenceThreshold: number
  originalVideoUrl: string
  originalFileName: string
  redactedVideoUrl: string | null
  status: JobStatus
  errorMessage: string | null
  detectionPreviews: DetectionPreview[] | null
  totalProcessingTimeMs: number | null
  objectsDetected: number | null
  framesProcessed: number | null
  createdAt: string
  updatedAt: string
}

export interface SubmitJobResponse {
  jobId: string
  status: string
  translationWarning: string | null
}

export interface CreateRedactJobRequest {
  file: File
  prompt: string
  detectionPrompt?: string
  redactionStyle: RedactionStyle
  confidenceThreshold: number
}

export interface TranslatePromptResponse {
  detectionPrompt: string
  isFallback: boolean
  warning: string | null
}
