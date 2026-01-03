<script setup lang="ts">
import { computed, ref, onMounted } from 'vue'
import { Doughnut } from 'vue-chartjs'
import {
  Chart as ChartJS,
  ArcElement,
  Tooltip,
  Legend
} from 'chart.js'
import type { CategoryDataPoint } from '@/types/analytics'

ChartJS.register(ArcElement, Tooltip, Legend)

const props = withDefaults(defineProps<{
  /** Chart title */
  title: string
  /** Category data */
  data: CategoryDataPoint[]
  /** Show legend */
  showLegend?: boolean
  /** Show as bar chart instead of doughnut */
  variant?: 'doughnut' | 'bar'
  /** Color palette */
  colors?: string[]
  /** Height of the chart */
  height?: number
  /** Loading state */
  loading?: boolean
}>(), {
  showLegend: true,
  variant: 'doughnut',
  height: 200,
  loading: false
})

const isDark = ref(false)

const defaultColors = [
  '#6366f1', // Indigo
  '#8b5cf6', // Purple
  '#ec4899', // Pink
  '#f59e0b', // Amber
  '#10b981', // Emerald
  '#06b6d4', // Cyan
  '#3b82f6', // Blue
  '#f97316'  // Orange
]

onMounted(() => {
  isDark.value = document.documentElement.classList.contains('dark')

  const observer = new MutationObserver(() => {
    isDark.value = document.documentElement.classList.contains('dark')
  })
  observer.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] })
})

const colors = computed(() => {
  return props.colors || props.data.map((d, i) => d.color || defaultColors[i % defaultColors.length])
})

const chartData = computed(() => ({
  labels: props.data.map(d => d.category),
  datasets: [{
    data: props.data.map(d => d.value),
    backgroundColor: colors.value,
    borderColor: isDark.value ? '#1f2937' : '#fff',
    borderWidth: 2,
    hoverOffset: 4
  }]
}))

const chartOptions = computed(() => ({
  responsive: true,
  maintainAspectRatio: false,
  cutout: '60%',
  plugins: {
    legend: {
      display: false
    },
    tooltip: {
      backgroundColor: isDark.value ? '#1f2937' : '#fff',
      titleColor: isDark.value ? '#f3f4f6' : '#111827',
      bodyColor: isDark.value ? '#d1d5db' : '#4b5563',
      borderColor: isDark.value ? '#374151' : '#e5e7eb',
      borderWidth: 1,
      padding: 12,
      callbacks: {
        label: (context: { label: string; parsed: number; dataset: { data: number[] } }) => {
          const total = context.dataset.data.reduce((a: number, b: number) => a + b, 0)
          const percentage = ((context.parsed / total) * 100).toFixed(1)
          return `${context.label}: ${context.parsed.toLocaleString()} (${percentage}%)`
        }
      }
    }
  }
}))

const total = computed(() => props.data.reduce((sum, d) => sum + d.value, 0))
</script>

<template>
  <div class="card p-4">
    <h3 class="text-sm font-medium text-gray-700 dark:text-gray-300">
      {{ title }}
    </h3>

    <!-- Loading state -->
    <div
      v-if="loading"
      class="mt-4 flex items-center justify-center"
      :style="{ height: `${height}px` }"
    >
      <svg class="h-8 w-8 animate-spin text-aegis-500" viewBox="0 0 24 24">
        <circle
          class="opacity-25"
          cx="12"
          cy="12"
          r="10"
          stroke="currentColor"
          stroke-width="4"
          fill="none"
        />
        <path
          class="opacity-75"
          fill="currentColor"
          d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"
        />
      </svg>
    </div>

    <!-- Content -->
    <div v-else class="mt-4 flex items-center gap-4">
      <!-- Chart -->
      <div class="relative flex-shrink-0" :style="{ width: `${height}px`, height: `${height}px` }">
        <Doughnut :data="chartData" :options="chartOptions" />
        <!-- Center total -->
        <div class="absolute inset-0 flex flex-col items-center justify-center">
          <span class="text-xs text-gray-500 dark:text-gray-400">Total</span>
          <span class="text-lg font-bold text-gray-900 dark:text-white">
            {{ total.toLocaleString() }}
          </span>
        </div>
      </div>

      <!-- Legend -->
      <div v-if="showLegend" class="flex-1 space-y-2">
        <div
          v-for="(item, index) in data"
          :key="item.category"
          class="flex items-center justify-between text-sm"
        >
          <div class="flex items-center gap-2">
            <span
              class="h-3 w-3 rounded-full"
              :style="{ backgroundColor: colors[index] }"
            />
            <span class="text-gray-600 dark:text-gray-400">
              {{ item.category }}
            </span>
          </div>
          <div class="flex items-center gap-2">
            <span class="font-medium text-gray-900 dark:text-white">
              {{ item.value.toLocaleString() }}
            </span>
            <span class="text-xs text-gray-400">
              {{ item.percentage?.toFixed(1) || ((item.value / total) * 100).toFixed(1) }}%
            </span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
