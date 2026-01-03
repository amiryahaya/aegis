<script setup lang="ts">
import { ref, computed } from 'vue'
import { usePerformanceMonitor } from '@/composables/usePerformanceMonitor'
import {
  ChartBarIcon,
  CpuChipIcon,
  ClockIcon,
  ExclamationTriangleIcon
} from '@heroicons/vue/24/outline'

const props = withDefaults(defineProps<{
  /** Show detailed metrics */
  detailed?: boolean
  /** Position of the monitor */
  position?: 'top-left' | 'top-right' | 'bottom-left' | 'bottom-right'
  /** Collapsed by default */
  collapsed?: boolean
}>(), {
  detailed: false,
  position: 'bottom-right',
  collapsed: true
})

const isExpanded = ref(!props.collapsed)

const { metrics, score, grade, isMonitoring, start, stop } = usePerformanceMonitor({
  trackFps: true,
  trackMemory: true,
  trackLongTasks: true,
  trackWebVitals: true
})

// Position classes
const positionClasses = computed(() => {
  const positions = {
    'top-left': 'top-4 left-4',
    'top-right': 'top-4 right-4',
    'bottom-left': 'bottom-4 left-4',
    'bottom-right': 'bottom-4 right-4'
  }
  return positions[props.position]
})

// Grade color
const gradeColor = computed(() => {
  const colors: Record<string, string> = {
    A: 'text-green-500 bg-green-100 dark:bg-green-900/30',
    B: 'text-blue-500 bg-blue-100 dark:bg-blue-900/30',
    C: 'text-yellow-500 bg-yellow-100 dark:bg-yellow-900/30',
    D: 'text-orange-500 bg-orange-100 dark:bg-orange-900/30',
    F: 'text-red-500 bg-red-100 dark:bg-red-900/30'
  }
  return colors[grade.value] || colors.F
})

// FPS color
const fpsColor = computed(() => {
  const fps = metrics.value.fps
  if (fps >= 55) return 'text-green-500'
  if (fps >= 30) return 'text-yellow-500'
  return 'text-red-500'
})

function toggleExpanded() {
  isExpanded.value = !isExpanded.value
}
</script>

<template>
  <div
    class="fixed z-50"
    :class="positionClasses"
  >
    <div
      class="rounded-lg border border-gray-200 bg-white/95 shadow-lg backdrop-blur-sm transition-all dark:border-gray-700 dark:bg-gray-800/95"
      :class="isExpanded ? 'w-64' : 'w-auto'"
    >
      <!-- Collapsed view -->
      <button
        v-if="!isExpanded"
        class="flex items-center gap-2 p-2"
        @click="toggleExpanded"
      >
        <span
          class="flex h-6 w-6 items-center justify-center rounded text-sm font-bold"
          :class="gradeColor"
        >
          {{ grade }}
        </span>
        <span class="text-xs font-mono" :class="fpsColor">
          {{ metrics.fps }} FPS
        </span>
      </button>

      <!-- Expanded view -->
      <div v-else>
        <!-- Header -->
        <div
          class="flex items-center justify-between border-b border-gray-200 p-2 dark:border-gray-700"
        >
          <div class="flex items-center gap-2">
            <ChartBarIcon class="h-4 w-4 text-gray-400" />
            <span class="text-sm font-medium text-gray-700 dark:text-gray-300">
              Performance
            </span>
          </div>
          <div class="flex items-center gap-2">
            <span
              class="flex h-6 w-6 items-center justify-center rounded text-sm font-bold"
              :class="gradeColor"
            >
              {{ grade }}
            </span>
            <button
              class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200"
              @click="toggleExpanded"
            >
              <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>
        </div>

        <!-- Metrics -->
        <div class="p-3 space-y-2">
          <!-- Score bar -->
          <div class="mb-3">
            <div class="flex items-center justify-between text-xs mb-1">
              <span class="text-gray-500 dark:text-gray-400">Score</span>
              <span class="font-medium" :class="gradeColor.split(' ')[0]">
                {{ score }}/100
              </span>
            </div>
            <div class="h-2 w-full rounded-full bg-gray-200 dark:bg-gray-700">
              <div
                class="h-2 rounded-full transition-all"
                :class="{
                  'bg-green-500': score >= 90,
                  'bg-blue-500': score >= 80 && score < 90,
                  'bg-yellow-500': score >= 70 && score < 80,
                  'bg-orange-500': score >= 60 && score < 70,
                  'bg-red-500': score < 60
                }"
                :style="{ width: `${score}%` }"
              />
            </div>
          </div>

          <!-- FPS -->
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2">
              <ClockIcon class="h-4 w-4 text-gray-400" />
              <span class="text-xs text-gray-500 dark:text-gray-400">FPS</span>
            </div>
            <span class="text-sm font-mono font-medium" :class="fpsColor">
              {{ metrics.fps }}
            </span>
          </div>

          <!-- Frame Time -->
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2">
              <ClockIcon class="h-4 w-4 text-gray-400" />
              <span class="text-xs text-gray-500 dark:text-gray-400">Frame Time</span>
            </div>
            <span class="text-sm font-mono text-gray-700 dark:text-gray-300">
              {{ metrics.frameTime.toFixed(1) }}ms
            </span>
          </div>

          <!-- Memory (if available) -->
          <div v-if="metrics.memoryUsage !== null" class="flex items-center justify-between">
            <div class="flex items-center gap-2">
              <CpuChipIcon class="h-4 w-4 text-gray-400" />
              <span class="text-xs text-gray-500 dark:text-gray-400">Memory</span>
            </div>
            <span class="text-sm font-mono text-gray-700 dark:text-gray-300">
              {{ metrics.memoryUsage }}MB
            </span>
          </div>

          <!-- Long Tasks -->
          <div v-if="metrics.longTasks > 0" class="flex items-center justify-between">
            <div class="flex items-center gap-2">
              <ExclamationTriangleIcon class="h-4 w-4 text-yellow-500" />
              <span class="text-xs text-gray-500 dark:text-gray-400">Long Tasks</span>
            </div>
            <span class="text-sm font-mono text-yellow-600 dark:text-yellow-400">
              {{ metrics.longTasks }}
            </span>
          </div>

          <!-- Web Vitals (detailed mode) -->
          <template v-if="detailed">
            <div class="border-t border-gray-200 pt-2 mt-2 dark:border-gray-700">
              <p class="text-xs font-medium text-gray-500 dark:text-gray-400 mb-2">
                Core Web Vitals
              </p>

              <!-- LCP -->
              <div v-if="metrics.lcp !== null" class="flex items-center justify-between">
                <span class="text-xs text-gray-500 dark:text-gray-400">LCP</span>
                <span
                  class="text-sm font-mono"
                  :class="{
                    'text-green-500': metrics.lcp < 2500,
                    'text-yellow-500': metrics.lcp >= 2500 && metrics.lcp < 4000,
                    'text-red-500': metrics.lcp >= 4000
                  }"
                >
                  {{ (metrics.lcp / 1000).toFixed(2) }}s
                </span>
              </div>

              <!-- CLS -->
              <div class="flex items-center justify-between">
                <span class="text-xs text-gray-500 dark:text-gray-400">CLS</span>
                <span
                  class="text-sm font-mono"
                  :class="{
                    'text-green-500': metrics.cls < 0.1,
                    'text-yellow-500': metrics.cls >= 0.1 && metrics.cls < 0.25,
                    'text-red-500': metrics.cls >= 0.25
                  }"
                >
                  {{ metrics.cls.toFixed(3) }}
                </span>
              </div>

              <!-- FID -->
              <div v-if="metrics.fid !== null" class="flex items-center justify-between">
                <span class="text-xs text-gray-500 dark:text-gray-400">FID</span>
                <span
                  class="text-sm font-mono"
                  :class="{
                    'text-green-500': metrics.fid < 100,
                    'text-yellow-500': metrics.fid >= 100 && metrics.fid < 300,
                    'text-red-500': metrics.fid >= 300
                  }"
                >
                  {{ metrics.fid }}ms
                </span>
              </div>
            </div>
          </template>

          <!-- Toggle monitoring -->
          <div class="border-t border-gray-200 pt-2 mt-2 dark:border-gray-700">
            <button
              class="w-full text-xs py-1 px-2 rounded"
              :class="isMonitoring
                ? 'bg-red-100 text-red-700 hover:bg-red-200 dark:bg-red-900/30 dark:text-red-300'
                : 'bg-green-100 text-green-700 hover:bg-green-200 dark:bg-green-900/30 dark:text-green-300'"
              @click="isMonitoring ? stop() : start()"
            >
              {{ isMonitoring ? 'Stop Monitoring' : 'Start Monitoring' }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
