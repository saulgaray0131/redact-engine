import { useMutation } from '@tanstack/react-query';
import { translatePrompt } from '@/api/prompts';

export function usePromptTranslation() {
  return useMutation({
    mutationFn: (prompt: string) => translatePrompt(prompt),
  });
}
