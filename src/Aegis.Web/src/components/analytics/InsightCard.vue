<script setup lang="ts">
import { computed } from 'vue'
import {
  InformationCircleIcon,
  ExclamationTriangleIcon,
  ExclamationCircleIcon,
  CheckCircleIcon,
  LightBulbIcon
} from '@heroicons/vue/24/outline'
import type { Insight, InsightSeverity } from '@/types/analytics'

const props = defineProps<{
  insight: Insight
}>()

const severityConfig = computed(() => {
  const configs: Record<InsightSeverity, {
    icon: object
    bgColor: string
    textColor: string
    borderColor: string
  }> = {
    info: {
      icon: InformationCircleIcon,
      bgColor: 'bg-blue-50 dark:bg-blue-900/20',
      textColor: 'text-blue-700 dark:text-blue-300',
      borderColor: 'border-blue-200 dark:border-blue-800'
    },
    warning: {
      icon: ExclamationTriangleIcon,
      bgColor: 'bg-yellow-50 dark:bg-yellow-900/20',
      textColor: 'text-yellow-700 dark:text-yellow-300',
      borderColor: 'border-yellow-200 dark:border-yellow-800'
    },
    critical: {
      icon: ExclamationCircleIcon,
      bgColor: 'bg-red-50 dark:bg-red-900/20',
      textColor: 'text-red-700 dark:text-red-300',
      borderColor: 'border-red-200 dark:border-red-800'
    },
    success: {
      icon: CheckCircleIcon,
      bgColor: 'bg-green-50 dark:bg-green-900/20',
      textColor: 'text-green-700 dark:text-green-300',
      borderColor: 'border-green-200 dark:border-green-800'
    }
  }
  return configs[props.insight.severity]
})

const typeLabel = computed(() => {
  const labels: Record<string, string> = {
    performance: 'Performance',
    usage: 'Usage',
    quality: 'Quality',
    security: 'Security',
    cost: 'Cost',
    engagement: 'Engagement'
  }
  return labels[props.insight.type] || props.insight.type
})

function formatDate(dateStr: string): string {
  const date = new Date(dateStr)
  return date.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}
</script>

<template>
  <div
    class="rounded-lg border p-4"
    :class="[severityConfig.bgColor, severityConfig.borderColor]"
  >
    <div class="flex items-start gap-3">
      <component
        :is="severityConfig.icon"
        class="h-5 w-5 shrink-0 mt-0.5"
        :class="severityConfig.textColor"
      />

      <div class="flex-1 min-w-0">
        <div class="flex items-center gap-2 flex-wrap">
          <h4
            class="font-medium"
            :class="severityConfig.textColor"
          >
            {{ insight.title }}
          </h4>
          <span
            class="inline-flex rounded-full px-2 py-0.5 text-xs font-medium"
            :class="severityConfig.textColor"
          >
            {{ typeLabel }}
          </span>
        </div>

        <p class="mt-1 text-sm text-gray-600 dark:text-gray-400">
          {{ insight.description }}
        </p>

        <!-- Metric value if present -->
        <div v-if="insight.value !== undefined" class="mt-2 flex items-center gap-2">
          <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
            {{ insight.metric }}:
          </span>
          <span class="text-sm font-bold" :class="severityConfig.textColor">
            {{ insight.value }}
          </span>
          <span v-if="insight.threshold" class="text-xs text-gray-500 dark:text-gray-400">
            (threshold: {{ insight.threshold }})
          </span>
        </div>

        <!-- Recommendation -->
        <div
          v-if="insight.recommendation"
          class="mt-3 flex items-start gap-2 rounded-md bg-white/50 p-2 dark:bg-gray-800/50"
        >
          <LightBulbIcon class="h-4 w-4 shrink-0 text-yellow-500" />
          <p class="text-xs text-gray-600 dark:text-gray-400">
            {{ insight.recommendation }}
          </p>
        </div>

        <p class="mt-2 text-xs text-gray-400 dark:text-gray-500">
          {{ formatDate(insight.createdAt) }}
        </p>
      </div>
    </div>
  </div>
</template>
