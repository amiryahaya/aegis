<script setup lang="ts">
import { computed } from 'vue'
import { Line } from 'vue-chartjs'
import {
  Chart as ChartJS,
  Title,
  Tooltip,
  Legend,
  LineElement,
  CategoryScale,
  LinearScale,
  PointElement,
  Filler
} from 'chart.js'

// Register Chart.js components
ChartJS.register(
  Title,
  Tooltip,
  Legend,
  LineElement,
  CategoryScale,
  LinearScale,
  PointElement,
  Filler
)

interface Props {
  labels: string[]
  datasets: {
    label: string
    data: number[]
    borderColor?: string
    backgroundColor?: string
    fill?: boolean
    tension?: number
  }[]
  title?: string
  height?: number
}

const props = withDefaults(defineProps<Props>(), {
  title: '',
  height: 300
})

const isDark = computed(() => {
  return document.documentElement.classList.contains('dark')
})

const chartData = computed(() => ({
  labels: props.labels,
  datasets: props.datasets.map((ds, index) => ({
    ...ds,
    borderColor: ds.borderColor || getDefaultColor(index),
    backgroundColor: ds.backgroundColor || getDefaultColor(index, 0.2),
    fill: ds.fill ?? true,
    tension: ds.tension ?? 0.4,
    pointRadius: 3,
    pointHoverRadius: 5
  }))
}))

const chartOptions = computed(() => ({
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
      grid: {
        color: isDark.value ? '#374151' : '#e5e7eb'
      },
      ticks: {
        color: isDark.value ? '#9ca3af' : '#4b5563'
      }
    },
    y: {
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
    <Line :data="chartData" :options="chartOptions" />
  </div>
</template>
