import { Link } from 'react-router-dom'
import { Shield, Plus, List } from 'lucide-react'
import { Button } from '@/components/ui/button'

export default function HomePage() {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center px-4">
      <div className="flex max-w-lg flex-col items-center gap-6 text-center">
        <div className="flex items-center gap-3">
          <Shield className="size-10 text-primary" />
          <h1 className="text-4xl font-bold tracking-tight">RedactEngine</h1>
        </div>

        <p className="text-lg text-muted-foreground">
          Upload images or videos and redact sensitive content using
          natural-language prompts.
        </p>

        <div className="flex gap-3">
          <Button render={<Link to="/app/new" />}>
            <Plus className="size-4" />
            Create New Job
          </Button>
          <Button variant="outline" render={<Link to="/app/jobs" />}>
            <List className="size-4" />
            View Jobs
          </Button>
        </div>
      </div>
    </div>
  )
}
