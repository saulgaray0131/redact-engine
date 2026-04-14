import { Settings } from 'lucide-react'
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { useTheme } from '@/hooks/use-theme'
import { useQuery, useMutation } from '@tanstack/react-query'
import { getHealthDetailedOptions, postSamplesPubsubMutation } from '@/client/@tanstack/react-query.gen'
import { appConfig } from '@/config/app-config'

export default function SettingsPage() {
  const { theme, setTheme } = useTheme()
  const health = useQuery(getHealthDetailedOptions())
  const samplePubSub = useMutation({
    ...postSamplesPubsubMutation(),
  })

  return (
    <div className="mx-auto flex max-w-2xl flex-col gap-6">
      <div className="flex items-center gap-2">
        <Settings className="size-6" />
        <h1 className="text-2xl font-bold tracking-tight">Settings</h1>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Authentication</CardTitle>
          <CardDescription>
            Auth is currently disabled. Configure Auth0 or custom JWT provider
            here.
          </CardDescription>
        </CardHeader>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Backend</CardTitle>
          <CardDescription>
            API endpoint: {appConfig.apiBaseUrl}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex flex-col gap-3">
            <div className="flex items-center gap-2">
              <span className="text-sm font-medium">Status:</span>
              {health.isLoading && <span className="text-sm text-muted-foreground">Checking...</span>}
              {health.isError && <span className="text-sm text-destructive">Unreachable</span>}
              {health.data && (
                <span className="text-sm text-green-600 dark:text-green-400">
                  {health.data.status} — v{health.data.version} ({health.data.environment})
                </span>
              )}
            </div>
            <Button variant="outline" size="sm" className="w-fit" onClick={() => health.refetch()}>
              Refresh
            </Button>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Sample Pub/Sub</CardTitle>
          <CardDescription>
            Test the Dapr pub/sub integration by publishing a sample message.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex flex-col gap-3">
            <Button
              variant="outline"
              size="sm"
              className="w-fit"
              disabled={samplePubSub.isPending}
              onClick={() => samplePubSub.mutate({ body: { message: 'Hello from the frontend!', source: 'RedactEngine.Web' } })}
            >
              {samplePubSub.isPending ? 'Publishing...' : 'Publish Test Message'}
            </Button>
            {samplePubSub.isSuccess && (
              <p className="text-sm text-green-600 dark:text-green-400">
                Published to {samplePubSub.data.topic} (event: {samplePubSub.data.eventId})
              </p>
            )}
            {samplePubSub.isError && (
              <p className="text-sm text-destructive">
                Failed: {samplePubSub.error.message}
              </p>
            )}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Theme</CardTitle>
          <CardDescription>Choose your preferred color scheme.</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex gap-2">
            {(['light', 'dark', 'system'] as const).map((t) => (
              <Button
                key={t}
                variant={theme === t ? 'default' : 'outline'}
                size="sm"
                onClick={() => setTheme(t)}
              >
                {t.charAt(0).toUpperCase() + t.slice(1)}
              </Button>
            ))}
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
