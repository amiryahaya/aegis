<script setup lang="ts">
import { computed } from 'vue'
import { Bar } from 'vue-chartjs'
import {
  Chart as ChartJS,
  Title,
  Tooltip,
  Legend,
  BarElement,
  CategoryScale,
  LinearScale
} from 'chart.js'

// Register Chart.js components
ChartJS.register(
  Title,
  Tooltip,
  Legend,
  BarElement,
  CategoryScale,
  LinearScale
)

interface Props {
  labels: string[]
  datasets: {
    label: string
    data: number[]
    backgroundColor?: string | string[]
    borderColor?: string | string[]
    borderWidth?: number
  }[]
  title?: string
  height?: number
  horizontal?: boolean
  stacked?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  title: '',
  height: 300,
  horizontal: false,
  stacked: false
})

const isDark = computed(() => {
  return document.documentElement.classList.contains('dark')
})

const chartData = computed(() => ({
  labels: props.labels,
  datasets: props.datasets.map((ds, index) => ({
    ...ds,
    backgroundColor: ds.backgroundColor || getDefaultColor(index, 0.7),
    borderColor: ds.borderColor || getDefaultColor(index),
    borderWidth: ds.borderWidth ?? 1,
    borderRadius: 4
  }))
}))

const chartOptions = computed(() => ({
  indexAxis: props.horizontal ? 'y' as const : 'x' as const,
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: {
      position: 'top' as const,
      labels: {
        color: isDark.value ? '#9ca3af' : '#4b5563',
        usePointStyle: true,
        padding: 20
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
  },
  scales: {
    x: {
      stacked: props.stacked,
      grid: {
        color: isDark.value ? '#374151' : '#e5e7eb'
      },
      ticks: {
        color: isDark.value ? '#9ca3af' : '#4b5563'
      }
    },
    y: {
      stacked: props.stacked,
      grid: {
        color: isDark.value ? '#374151' : '#e5e7eb'
      },
      ticks: {
        color: isDark.value ? '#9ca3af' : '#4b5563'
      }
    }
  }
}))

function getDefaultColor(index: number, alpha = 1): string {
  const colors = [
    `rgba(99, 102, 241, ${alpha})`,   // indigo
    `rgba(16, 185, 129, ${alpha})`,   // emerald
    `rgba(245, 158, 11, ${alpha})`,   // amber
    `rgba(239, 68, 68, ${alpha})`,    // red
    `rgba(139, 92, 246, ${alpha})`,   // violet
    `rgba(6, 182, 212, ${alpha})`     // cyan
  ]
  return colors[index % colors.length]
}
</script>

<template>
  <div :style="{ height: `${height}px` }">
    <Bar :data="chartData" :options="chartOptions" />
  </div>
</template>
