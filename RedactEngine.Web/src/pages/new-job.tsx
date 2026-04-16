import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Upload, FileVideo } from 'lucide-react'
import { Radio } from '@base-ui/react/radio'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { Card, CardHeader, CardTitle, CardDescription, CardContent, CardFooter } from '@/components/ui/card'
import { RadioGroup } from '@/components/ui/radio-group'
import { Slider } from '@/components/ui/slider'
import { Separator } from '@/components/ui/separator'
import { useCreateRedactJob } from '@/hooks/use-redact-jobs'
import { cn } from '@/lib/utils'
import type { RedactionStyle } from '@/types'

const REDACTION_STYLES: { value: RedactionStyle; label: string; description: string }[] = [
  { value: 'Blur', label: 'Blur', description: 'Gaussian blur' },
  { value: 'Pixelate', label: 'Pixelate', description: 'Mosaic blocks' },
  { value: 'Fill', label: 'Fill', description: 'Solid color fill' },
]

export default function NewJobPage() {
  const [file, setFile] = useState<File | null>(null)
  const [prompt, setPrompt] = useState('')
  const [redactionStyle, setRedactionStyle] = useState<RedactionStyle>('Blur')
  const [confidenceThreshold, setConfidenceThreshold] = useState(0.3)
  const [error, setError] = useState<string | null>(null)
  const navigate = useNavigate()
  const mutation = useCreateRedactJob()

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!file || !prompt.trim()) return

    setError(null)
    mutation.mutate(
      { file, prompt: prompt.trim(), redactionStyle, confidenceThreshold },
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

          <Separator />

          <div className="flex flex-col gap-2">
            <label className="text-sm font-medium">Redaction Style</label>
            <RadioGroup
              value={redactionStyle}
              onValueChange={(val) => setRedactionStyle(val as RedactionStyle)}
            >
              <div className="grid grid-cols-3 gap-2">
                {REDACTION_STYLES.map((style) => (
                  <Radio.Root
                    key={style.value}
                    value={style.value}
                    className={cn(
                      'flex cursor-pointer flex-col items-center gap-1 rounded-lg border border-input px-3 py-2.5 text-center transition-colors',
                      'hover:bg-muted',
                      'data-checked:border-primary data-checked:bg-primary/5 data-checked:ring-1 data-checked:ring-primary'
                    )}
                  >
                    <span className="text-sm font-medium">{style.label}</span>
                    <span className="text-xs text-muted-foreground">{style.description}</span>
                  </Radio.Root>
                ))}
              </div>
            </RadioGroup>
          </div>

          <div className="flex flex-col gap-2">
            <div className="flex items-center justify-between">
              <label className="text-sm font-medium">Confidence Threshold</label>
              <span className="text-sm tabular-nums text-muted-foreground">
                {Math.round(confidenceThreshold * 100)}%
              </span>
            </div>
            <Slider
              value={confidenceThreshold}
              onValueChange={(val) => setConfidenceThreshold(val as number)}
              min={0}
              max={1}
              step={0.05}
            />
            <p className="text-xs text-muted-foreground">
              Objects detected below this confidence level will be skipped. Lower values catch more objects but may include false positives.
            </p>
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
