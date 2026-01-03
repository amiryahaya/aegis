<script setup lang="ts">
import { computed, ref, onMounted } from 'vue'
import { Line } from 'vue-chartjs'
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend,
  Filler
} from 'chart.js'
import type { TimeSeriesDataPoint } from '@/types/analytics'

ChartJS.register(
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend,
  Filler
)

const props = withDefaults(defineProps<{
  /** Chart title */
  title: string
  /** Time series data */
  data: TimeSeriesDataPoint[]
  /** Chart color */
  color?: string
  /** Show area fill */
  fill?: boolean
  /** Format for values */
  valueFormat?: 'number' | 'percent' | 'duration' | 'currency'
  /** Show comparison line (previous period) */
  comparisonData?: TimeSeriesDataPoint[]
  /** Height of the chart */
  height?: number
  /** Loading state */
  loading?: boolean
}>(), {
  color: '#6366f1',
  fill: true,
  valueFormat: 'number',
  height: 200,
  loading: false
})

const isDark = ref(false)

onMounted(() => {
  isDark.value = document.documentElement.classList.contains('dark')

  // Watch for dark mode changes
  const observer = new MutationObserver(() => {
    isDark.value = document.documentElement.classList.contains('dark')
  })
  observer.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] })
})

const chartData = computed(() => ({
  labels: props.data.map(d => {
    const date = new Date(d.date)
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
  }),
  datasets: [
    {
      label: props.title,
      data: props.data.map(d => d.value),
      borderColor: props.color,
      backgroundColor: props.fill ? `${props.color}20` : 'transparent',
      fill: props.fill,
      tension: 0.4,
      pointRadius: 0,
      pointHoverRadius: 4,
      pointHoverBackgroundColor: props.color,
      pointHoverBorderColor: '#fff',
      pointHoverBorderWidth: 2
    },
    ...(props.comparisonData ? [{
      label: 'Previous Period',
      data: props.comparisonData.map(d => d.value),
      borderColor: isDark.value ? '#6b7280' : '#9ca3af',
      backgroundColor: 'transparent',
      fill: false,
      tension: 0.4,
      pointRadius: 0,
      borderDash: [5, 5]
    }] : [])
  ]
}))

const chartOptions = computed(() => ({
  responsive: true,
  maintainAspectRatio: false,
  interaction: {
    mode: 'index' as const,
    intersect: false
  },
  plugins: {
    legend: {
      display: props.comparisonData !== undefined,
      position: 'top' as const,
      labels: {
        usePointStyle: true,
        boxWidth: 6,
        color: isDark.value ? '#9ca3af' : '#6b7280'
      }
    },
    tooltip: {
      backgroundColor: isDark.value ? '#1f2937' : '#fff',
      titleColor: isDark.value ? '#f3f4f6' : '#111827',
      bodyColor: isDark.value ? '#d1d5db' : '#4b5563',
      borderColor: isDark.value ? '#374151' : '#e5e7eb',
      borderWidth: 1,
      padding: 12,
      displayColors: true,
      callbacks: {
        label: (context: { dataset: { label?: string }; parsed: { y: number | null } }) => {
          const value = context.parsed.y ?? 0
          switch (props.valueFormat) {
            case 'percent':
              return `${context.dataset.label}: ${value.toFixed(1)}%`
            case 'duration':
              return `${context.dataset.label}: ${value.toFixed(2)}s`
            case 'currency':
              return `${context.dataset.label}: $${value.toLocaleString()}`
            default:
              return `${context.dataset.label}: ${value.toLocaleString()}`
          }
        }
      }
    }
  },
  scales: {
    x: {
      grid: {
        display: false
      },
      ticks: {
        color: isDark.value ? '#6b7280' : '#9ca3af',
        maxTicksLimit: 7
      }
    },
    y: {
      beginAtZero: true,
      grid: {
        color: isDark.value ? '#374151' : '#f3f4f6'
      },
      ticks: {
        color: isDark.value ? '#6b7280' : '#9ca3af',
        callback: (value: string | number) => {
          const numValue = typeof value === 'string' ? parseFloat(value) : value
          switch (props.valueFormat) {
            case 'percent':
              return `${numValue}%`
            case 'duration':
              return `${numValue}s`
            case 'currency':
              return `$${numValue}`
            default:
              if (numValue >= 1000) return `${(numValue / 1000).toFixed(0)}K`
              return numValue
          }
        }
      }
    }
  }
}))
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

    <!-- Chart -->
    <div v-else class="mt-4" :style="{ height: `${height}px` }">
      <Line :data="chartData" :options="chartOptions" />
    </div>
  </div>
</template>
