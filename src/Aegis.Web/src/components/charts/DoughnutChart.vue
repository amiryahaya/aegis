<script setup lang="ts">
import { computed } from 'vue'
import { Doughnut } from 'vue-chartjs'
import {
  Chart as ChartJS,
  Title,
  Tooltip,
  Legend,
  ArcElement
} from 'chart.js'

// Register Chart.js components
ChartJS.register(
  Title,
  Tooltip,
  Legend,
  ArcElement
)

interface Props {
  labels: string[]
  data: number[]
  colors?: string[]
  title?: string
  height?: number
  cutout?: string
}

const props = withDefaults(defineProps<Props>(), {
  title: '',
  height: 300,
  cutout: '60%'
})

const isDark = computed(() => {
  return document.documentElement.classList.contains('dark')
})

const defaultColors = [
  'rgba(99, 102, 241, 0.8)',   // indigo
  'rgba(16, 185, 129, 0.8)',   // emerald
  'rgba(245, 158, 11, 0.8)',   // amber
  'rgba(239, 68, 68, 0.8)',    // red
  'rgba(139, 92, 246, 0.8)',   // violet
  'rgba(6, 182, 212, 0.8)',    // cyan
  'rgba(236, 72, 153, 0.8)',   // pink
  'rgba(34, 197, 94, 0.8)'     // green
]

const chartData = computed(() => ({
  labels: props.labels,
  datasets: [{
    data: props.data,
    backgroundColor: props.colors || defaultColors.slice(0, props.data.length),
    borderColor: isDark.value ? '#1f2937' : '#ffffff',
    borderWidth: 2,
    hoverOffset: 4
  }]
}))

const chartOptions = computed(() => ({
  responsive: true,
  maintainAspectRatio: false,
  cutout: props.cutout,
  plugins: {
    legend: {
      position: 'right' as const,
      labels: {
        color: isDark.value ? '#9ca3af' : '#4b5563',
        usePointStyle: true,
        padding: 15,
        font: {
          size: 12
        }
      }
    },
    title: {
      display: !!props.title,
      text: props.title,
      color: isDark.value ? '#f3f4f6' : '#111827',
      font: {
        size: 16,
        weight: 'bold' as const
      }
    },
    tooltip: {
      backgroundColor: isDark.value ? '#374151' : '#ffffff',
      titleColor: isDark.value ? '#f3f4f6' : '#111827',
      bodyColor: isDark.value ? '#d1d5db' : '#4b5563',
      borderColor: isDark.value ? '#4b5563' : '#e5e7eb',
      borderWidth: 1
    }
  }
}))
</script>

<template>
  <div :style="{ height: `${height}px` }">
    <Doughnut :data="chartData" :options="chartOptions" />
  </div>
</template>
