import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Upload, FileVideo } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { Card, CardHeader, CardTitle, CardDescription, CardContent, CardFooter } from '@/components/ui/card'
import { useCreateRedactJob } from '@/hooks/use-redact-jobs'

export default function NewJobPage() {
  const [file, setFile] = useState<File | null>(null)
  const [prompt, setPrompt] = useState('')
  const [error, setError] = useState<string | null>(null)
  const navigate = useNavigate()
  const mutation = useCreateRedactJob()

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!file || !prompt.trim()) return

    setError(null)
    mutation.mutate(
      { file, prompt: prompt.trim() },
      {
        onSuccess: (data) => {
          navigate(`/app/jobs/${data.jobId}`)
        },
        onError: (err) => {
          setError(err instanceof Error ? err.message : 'Failed to submit job. Please try again.')
        },
      }
    )
  }

  return (
    <form onSubmit={handleSubmit} className="mx-auto max-w-xl">
      <Card>
        <CardHeader>
          <CardTitle>New Redact Job</CardTitle>
          <CardDescription>
            Upload a video and describe what should be redacted.
          </CardDescription>
        </CardHeader>

        <CardContent className="flex flex-col gap-4">
          <div className="flex flex-col gap-2">
            <label className="text-sm font-medium">Video</label>
            <input
              type="file"
              accept="video/*"
              onChange={(e) => setFile(e.target.files?.[0] ?? null)}
              className="file:mr-3 file:rounded-lg file:border-0 file:bg-primary file:px-3 file:py-1.5 file:text-sm file:font-medium file:text-primary-foreground hover:file:bg-primary/80 text-sm text-muted-foreground"
            />
            {file && (
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <FileVideo className="size-4" />
                <span>{file.name}</span>
              </div>
            )}
          </div>

          <div className="flex flex-col gap-2">
            <label className="text-sm font-medium">
              Redaction Instructions
            </label>
            <Textarea
              value={prompt}
              onChange={(e) => setPrompt(e.target.value)}
              placeholder='Describe what should be redacted, e.g., "blur all faces" or "redact license plates"'
              rows={4}
            />
          </div>

          {error && (
            <p className="text-sm text-destructive">{error}</p>
          )}
        </CardContent>

        <CardFooter>
          <Button
            type="submit"
            disabled={!file || !prompt.trim() || mutation.isPending}
          >
            {mutation.isPending ? (
              <>Submitting...</>
            ) : (
              <>
                <Upload className="size-4" />
                Submit Job
              </>
            )}
          </Button>
        </CardFooter>
      </Card>
    </form>
  )
}
