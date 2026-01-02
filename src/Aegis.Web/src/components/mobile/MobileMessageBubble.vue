<script setup lang="ts">
import { computed } from 'vue'
import { SparklesIcon } from '@heroicons/vue/24/solid'
import type { SessionTurn } from '@/types'

const props = defineProps<{
  turn: SessionTurn
  isStreaming?: boolean
}>()

const emit = defineEmits<{
  'ask-follow-up': [question: string]
  'view-sources': []
}>()

const formattedTime = computed(() => {
  const date = new Date(props.turn.createdAt)
  return date.toLocaleTimeString('en-US', {
    hour: '2-digit',
    minute: '2-digit'
  })
})

const hasResponse = computed(() => props.turn.systemResponse || props.isStreaming)
</script>

<template>
  <div class="space-y-3 px-3">
    <!-- User message bubble -->
    <div class="flex justify-end">
      <div class="max-w-[85%]">
        <div
          class="rounded-2xl rounded-br-md bg-aegis-600 px-4 py-2.5 text-white"
        >
          <p class="text-[15px] leading-relaxed">{{ turn.userQuery }}</p>
        </div>
        <p class="mt-1 text-right text-xs text-gray-400">{{ formattedTime }}</p>
      </div>
    </div>

    <!-- Assistant response bubble -->
    <div v-if="hasResponse" class="flex justify-start">
      <div class="flex max-w-[85%] gap-2">
        <!-- Avatar -->
        <div class="shrink-0">
          <div class="flex h-7 w-7 items-center justify-center rounded-full bg-gray-100 dark:bg-gray-700">
            <SparklesIcon class="h-4 w-4 text-aegis-600 dark:text-aegis-400" />
          </div>
        </div>

        <div class="flex-1">
          <!-- Response content -->
          <div
            class="rounded-2xl rounded-tl-md bg-gray-100 px-4 py-2.5 dark:bg-gray-700"
          >
            <template v-if="turn.systemResponse">
              <p class="text-[15px] leading-relaxed text-gray-900 dark:text-white">
                {{ turn.systemResponse }}
              </p>
            </template>
            <template v-else-if="isStreaming">
              <div class="flex items-center gap-1">
                <span class="h-2 w-2 animate-bounce rounded-full bg-gray-400" style="animation-delay: 0ms" />
                <span class="h-2 w-2 animate-bounce rounded-full bg-gray-400" style="animation-delay: 150ms" />
                <span class="h-2 w-2 animate-bounce rounded-full bg-gray-400" style="animation-delay: 300ms" />
              </div>
            </template>
          </div>

          <!-- Sources badge -->
          <button
            v-if="turn.sources?.length"
            class="mt-2 inline-flex items-center gap-1 rounded-full bg-gray-50 px-3 py-1 text-xs text-gray-600 dark:bg-gray-800 dark:text-gray-400"
            @click="emit('view-sources')"
          >
            <svg class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
            </svg>
            {{ turn.sources.length }} source{{ turn.sources.length > 1 ? 's' : '' }}
          </button>

          <!-- Follow-up questions -->
          <div v-if="turn.followUpQuestions?.length" class="mt-3 flex flex-wrap gap-2">
            <button
              v-for="(question, idx) in turn.followUpQuestions"
              :key="idx"
              class="rounded-full border border-aegis-200 bg-white px-3 py-1.5 text-sm text-aegis-700 active:bg-aegis-50 dark:border-aegis-800 dark:bg-gray-800 dark:text-aegis-300"
              @click="emit('ask-follow-up', question)"
            >
              {{ question }}
            </button>
          </div>

          <!-- Processing time -->
          <p v-if="turn.processingTimeMs" class="mt-1 text-xs text-gray-400">
            {{ (turn.processingTimeMs / 1000).toFixed(1) }}s
          </p>
        </div>
      </div>
    </div>
  </div>
</template>
