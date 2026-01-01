<script setup lang="ts">
import { computed } from 'vue'
import { UserCircleIcon, SparklesIcon } from '@heroicons/vue/24/solid'
import type { SessionTurn } from '@/types'

const props = defineProps<{
  turn: SessionTurn
  isStreaming?: boolean
}>()

const formattedTime = computed(() => {
  const date = new Date(props.turn.createdAt)
  return date.toLocaleTimeString('en-US', {
    hour: '2-digit',
    minute: '2-digit'
  })
})
</script>

<template>
  <div class="space-y-4">
    <!-- User message -->
    <div class="flex gap-3">
      <div class="shrink-0">
        <UserCircleIcon class="h-8 w-8 text-gray-400" />
      </div>
      <div class="flex-1 min-w-0">
        <div class="flex items-center gap-2 mb-1">
          <span class="font-medium text-gray-900 dark:text-white">You</span>
          <span class="text-xs text-gray-400">{{ formattedTime }}</span>
        </div>
        <div class="prose-chat">
          {{ turn.userQuery }}
        </div>
      </div>
    </div>

    <!-- Assistant response -->
    <div v-if="turn.systemResponse || isStreaming" class="flex gap-3">
      <div class="shrink-0">
        <div class="flex h-8 w-8 items-center justify-center rounded-full bg-aegis-100 dark:bg-aegis-900/50">
          <SparklesIcon class="h-5 w-5 text-aegis-600 dark:text-aegis-400" />
        </div>
      </div>
      <div class="flex-1 min-w-0">
        <div class="flex items-center gap-2 mb-1">
          <span class="font-medium text-aegis-600 dark:text-aegis-400">AEGIS</span>
          <span v-if="turn.processingTimeMs" class="text-xs text-gray-400">
            {{ (turn.processingTimeMs / 1000).toFixed(1) }}s
          </span>
        </div>

        <!-- Response content -->
        <div class="prose-chat">
          <template v-if="turn.systemResponse">
            {{ turn.systemResponse }}
          </template>
          <template v-else-if="isStreaming">
            <div class="typing-indicator">
              <span></span>
              <span></span>
              <span></span>
            </div>
          </template>
        </div>

        <!-- Sources -->
        <div v-if="turn.sources?.length" class="mt-3">
          <details class="group">
            <summary class="flex cursor-pointer items-center gap-1 text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-300">
              <svg class="h-4 w-4 transition-transform group-open:rotate-90" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5l7 7-7 7" />
              </svg>
              {{ turn.sources.length }} source{{ turn.sources.length > 1 ? 's' : '' }}
            </summary>
            <div class="mt-2 space-y-2 pl-5">
              <div
                v-for="(source, idx) in turn.sources"
                :key="idx"
                class="rounded-lg border border-gray-200 bg-gray-50 p-3 text-sm dark:border-gray-700 dark:bg-gray-800"
              >
                <div class="flex items-center justify-between mb-1">
                  <span class="font-medium text-gray-700 dark:text-gray-300">
                    {{ source.documentName }}
                  </span>
                  <span class="text-xs text-gray-400">
                    {{ Math.round(source.relevanceScore * 100) }}% match
                  </span>
                </div>
                <p v-if="source.excerpt" class="text-gray-600 dark:text-gray-400 line-clamp-2">
                  {{ source.excerpt }}
                </p>
              </div>
            </div>
          </details>
        </div>

        <!-- Follow-up questions -->
        <div v-if="turn.followUpQuestions?.length" class="mt-3 flex flex-wrap gap-2">
          <button
            v-for="(question, idx) in turn.followUpQuestions"
            :key="idx"
            class="rounded-full border border-aegis-200 bg-aegis-50 px-3 py-1 text-sm text-aegis-700 transition-colors hover:bg-aegis-100 dark:border-aegis-800 dark:bg-aegis-900/30 dark:text-aegis-300 dark:hover:bg-aegis-900/50"
            @click="$emit('askFollowUp', question)"
          >
            {{ question }}
          </button>
        </div>

        <!-- Metrics -->
        <div v-if="turn.metrics" class="mt-2 flex flex-wrap gap-3 text-xs text-gray-400">
          <span>{{ turn.metrics.tokensUsed }} tokens</span>
          <span>{{ turn.metrics.sourcesUsed }}/{{ turn.metrics.sourcesRetrieved }} sources used</span>
          <span v-if="turn.metrics.cacheHit" class="text-green-500">Cache hit</span>
        </div>
      </div>
    </div>
  </div>
</template>
