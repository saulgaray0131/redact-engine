import type { TranslatePromptResponse } from '@/types';
import { apiClient } from './client';

export async function translatePrompt(prompt: string): Promise<TranslatePromptResponse> {
  return apiClient.post<TranslatePromptResponse>('/api/prompts/translate', { prompt });
}
