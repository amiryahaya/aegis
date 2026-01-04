<script setup lang="ts">
import { ref, computed } from 'vue'
import { UserCircleIcon, SparklesIcon } from '@heroicons/vue/24/solid'
import {
  ClipboardDocumentIcon,
  ArrowPathIcon,
  ShareIcon,
  HandThumbUpIcon,
  HandThumbDownIcon,
  CheckIcon
} from '@heroicons/vue/24/outline'
import { HandThumbUpIcon as HandThumbUpSolidIcon, HandThumbDownIcon as HandThumbDownSolidIcon } from '@heroicons/vue/24/solid'
import type { SessionTurn } from '@/types'

const props = defineProps<{
  turn: SessionTurn
  isStreaming?: boolean
}>()

const emit = defineEmits<{
  (e: 'askFollowUp', question: string): void
  (e: 'regenerate', turnId: string): void
  (e: 'feedback', turnId: string, type: 'positive' | 'negative'): void
  (e: 'share', turn: SessionTurn): void
}>()

const copied = ref(false)
const userFeedback = ref<'positive' | 'negative' | null>(null)
const showActions = ref(false)

const formattedTime = computed(() => {
  const date = new Date(props.turn.createdAt)
  return date.toLocaleTimeString('en-US', {
    hour: '2-digit',
    minute: '2-digit'
  })
})

async function copyResponse() {
  if (!props.turn.systemResponse) return

  try {
    await navigator.clipboard.writeText(props.turn.systemResponse)
    copied.value = true
    setTimeout(() => {
      copied.value = false
    }, 2000)
  } catch (error) {
    console.error('Failed to copy:', error)
  }
}

function handleRegenerate() {
  emit('regenerate', props.turn.id)
}

function handleFeedback(type: 'positive' | 'negative') {
  if (userFeedback.value === type) {
    userFeedback.value = null
  } else {
    userFeedback.value = type
    emit('feedback', props.turn.id, type)
  }
}

function handleShare() {
  emit('share', props.turn)
}
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
    <div
      v-if="turn.systemResponse || isStreaming"
      class="flex gap-3 group"
      @mouseenter="showActions = true"
      @mouseleave="showActions = false"
    >
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

        <!-- Quick Actions Toolbar -->
        <div
          v-if="turn.systemResponse && !isStreaming"
          class="mt-3 flex items-center gap-1 transition-opacity"
          :class="showActions ? 'opacity-100' : 'opacity-0 group-hover:opacity-100'"
        >
          <!-- Copy -->
          <button
            class="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs text-gray-500 hover:bg-gray-100 hover:text-gray-700 dark:text-gray-400 dark:hover:bg-gray-700 dark:hover:text-gray-200"
            @click="copyResponse"
          >
            <CheckIcon v-if="copied" class="h-4 w-4 text-green-500" />
            <ClipboardDocumentIcon v-else class="h-4 w-4" />
            <span>{{ copied ? 'Copied!' : 'Copy' }}</span>
          </button>

          <!-- Regenerate -->
          <button
            class="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs text-gray-500 hover:bg-gray-100 hover:text-gray-700 dark:text-gray-400 dark:hover:bg-gray-700 dark:hover:text-gray-200"
            @click="handleRegenerate"
          >
            <ArrowPathIcon class="h-4 w-4" />
            <span>Regenerate</span>
          </button>

          <!-- Share -->
          <button
            class="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs text-gray-500 hover:bg-gray-100 hover:text-gray-700 dark:text-gray-400 dark:hover:bg-gray-700 dark:hover:text-gray-200"
            @click="handleShare"
          >
            <ShareIcon class="h-4 w-4" />
            <span>Share</span>
          </button>

          <div class="w-px h-4 bg-gray-200 dark:bg-gray-700 mx-1" />

          <!-- Thumbs Up -->
          <button
            class="inline-flex items-center rounded-md p-1.5 transition-colors"
            :class="userFeedback === 'positive'
              ? 'text-green-600 bg-green-50 dark:bg-green-900/30'
              : 'text-gray-400 hover:bg-gray-100 hover:text-gray-600 dark:hover:bg-gray-700'"
            @click="handleFeedback('positive')"
          >
            <HandThumbUpSolidIcon v-if="userFeedback === 'positive'" class="h-4 w-4" />
            <HandThumbUpIcon v-else class="h-4 w-4" />
          </button>

          <!-- Thumbs Down -->
          <button
            class="inline-flex items-center rounded-md p-1.5 transition-colors"
            :class="userFeedback === 'negative'
              ? 'text-red-600 bg-red-50 dark:bg-red-900/30'
              : 'text-gray-400 hover:bg-gray-100 hover:text-gray-600 dark:hover:bg-gray-700'"
            @click="handleFeedback('negative')"
          >
            <HandThumbDownSolidIcon v-if="userFeedback === 'negative'" class="h-4 w-4" />
            <HandThumbDownIcon v-else class="h-4 w-4" />
          </button>
        </div>

        <!-- Sources -->
        <div v-if="turn.sources?.length" class="mt-3">
          <details class="group/sources">
            <summary class="flex cursor-pointer items-center gap-1 text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-300">
              <svg class="h-4 w-4 transition-transform group-open/sources:rotate-90" fill="none" viewBox="0 0 24 24" stroke="currentColor">
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
                  <div class="flex items-center gap-2">
                    <div class="w-16 h-1.5 bg-gray-200 dark:bg-gray-600 rounded-full overflow-hidden">
                      <div
                        class="h-full rounded-full"
                        :class="source.relevanceScore >= 0.8 ? 'bg-green-500' : source.relevanceScore >= 0.6 ? 'bg-yellow-500' : 'bg-orange-500'"
                        :style="{ width: `${source.relevanceScore * 100}%` }"
                      />
                    </div>
                    <span class="text-xs text-gray-400">
                      {{ Math.round(source.relevanceScore * 100) }}%
                    </span>
                  </div>
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
