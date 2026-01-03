<script setup lang="ts">
import { computed } from 'vue'
import {
  CheckCircleIcon,
  ExclamationTriangleIcon,
  XCircleIcon,
  ArrowPathIcon,
  CpuChipIcon,
  CircleStackIcon,
  ServerStackIcon,
  SignalIcon
} from '@heroicons/vue/24/outline'
import type { DetailedSystemHealth } from '@/types'
import { formatUptime } from '@/types/audit'

const props = defineProps<{
  health: DetailedSystemHealth | null
  isLoading?: boolean
}>()

const emit = defineEmits<{
  refresh: []
}>()

const statusConfig = computed(() => {
  if (!props.health) return { color: 'gray', label: 'Unknown', icon: ArrowPathIcon }

  switch (props.health.status) {
    case 'healthy':
      return { color: 'green', label: 'Healthy', icon: CheckCircleIcon }
    case 'degraded':
      return { color: 'yellow', label: 'Degraded', icon: ExclamationTriangleIcon }
    case 'unhealthy':
      return { color: 'red', label: 'Unhealthy', icon: XCircleIcon }
    default:
      return { color: 'gray', label: 'Unknown', icon: ArrowPathIcon }
  }
})

function getComponentStatusClasses(status: string): string {
  switch (status) {
    case 'healthy':
      return 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400'
    case 'degraded':
      return 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400'
    case 'unhealthy':
      return 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400'
    default:
      return 'bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-400'
  }
}

function getMetricColor(value: number, thresholds: { warning: number; critical: number }): string {
  if (value >= thresholds.critical) return 'text-red-500'
  if (value >= thresholds.warning) return 'text-yellow-500'
  return 'text-green-500'
}
</script>

<template>
  <div class="rounded-lg border bg-white p-6 dark:border-gray-700 dark:bg-gray-800">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <h3 class="text-lg font-semibold text-gray-900 dark:text-white">System Health</h3>
      <button
        class="rounded-lg p-2 text-gray-400 hover:bg-gray-100 hover:text-gray-600 dark:hover:bg-gray-700"
        :disabled="isLoading"
        @click="emit('refresh')"
      >
        <ArrowPathIcon class="h-5 w-5" :class="{ 'animate-spin': isLoading }" />
      </button>
    </div>

    <!-- Loading State -->
    <div v-if="isLoading && !health" class="mt-4 space-y-4">
      <div class="h-16 animate-pulse rounded-lg bg-gray-200 dark:bg-gray-700" />
      <div class="grid grid-cols-2 gap-4">
        <div v-for="i in 4" :key="i" class="h-12 animate-pulse rounded bg-gray-200 dark:bg-gray-700" />
      </div>
    </div>

    <template v-else-if="health">
      <!-- Overall Status -->
      <div
        class="mt-4 flex items-center gap-4 rounded-lg p-4"
        :class="{
          'bg-green-50 dark:bg-green-900/20': statusConfig.color === 'green',
          'bg-yellow-50 dark:bg-yellow-900/20': statusConfig.color === 'yellow',
          'bg-red-50 dark:bg-red-900/20': statusConfig.color === 'red',
          'bg-gray-50 dark:bg-gray-700/50': statusConfig.color === 'gray'
        }"
      >
        <component
          :is="statusConfig.icon"
          class="h-10 w-10"
          :class="{
            'text-green-500': statusConfig.color === 'green',
            'text-yellow-500': statusConfig.color === 'yellow',
            'text-red-500': statusConfig.color === 'red',
            'text-gray-400': statusConfig.color === 'gray'
          }"
        />
        <div>
          <p
            class="text-lg font-semibold"
            :class="{
              'text-green-800 dark:text-green-200': statusConfig.color === 'green',
              'text-yellow-800 dark:text-yellow-200': statusConfig.color === 'yellow',
              'text-red-800 dark:text-red-200': statusConfig.color === 'red',
              'text-gray-800 dark:text-gray-200': statusConfig.color === 'gray'
            }"
          >
            {{ statusConfig.label }}
          </p>
          <p class="text-sm text-gray-500 dark:text-gray-400">
            Uptime: {{ formatUptime(health.uptime) }}
          </p>
        </div>
      </div>

      <!-- Key Metrics -->
      <div class="mt-4 grid grid-cols-2 gap-4 sm:grid-cols-4">
        <div class="rounded-lg border p-3 dark:border-gray-700">
          <div class="flex items-center gap-2">
            <CpuChipIcon class="h-5 w-5 text-gray-400" />
            <span class="text-sm text-gray-500 dark:text-gray-400">CPU</span>
          </div>
          <p
            class="mt-1 text-xl font-semibold"
            :class="getMetricColor(health.metrics.cpu, { warning: 70, critical: 90 })"
          >
            {{ health.metrics.cpu.toFixed(1) }}%
          </p>
        </div>

        <div class="rounded-lg border p-3 dark:border-gray-700">
          <div class="flex items-center gap-2">
            <CircleStackIcon class="h-5 w-5 text-gray-400" />
            <span class="text-sm text-gray-500 dark:text-gray-400">Memory</span>
          </div>
          <p
            class="mt-1 text-xl font-semibold"
            :class="getMetricColor(health.metrics.memory, { warning: 75, critical: 90 })"
          >
            {{ health.metrics.memory.toFixed(1) }}%
          </p>
        </div>

        <div class="rounded-lg border p-3 dark:border-gray-700">
          <div class="flex items-center gap-2">
            <ServerStackIcon class="h-5 w-5 text-gray-400" />
            <span class="text-sm text-gray-500 dark:text-gray-400">Disk</span>
          </div>
          <p
            class="mt-1 text-xl font-semibold"
            :class="getMetricColor(health.metrics.disk, { warning: 80, critical: 95 })"
          >
            {{ health.metrics.disk.toFixed(1) }}%
          </p>
        </div>

        <div class="rounded-lg border p-3 dark:border-gray-700">
          <div class="flex items-center gap-2">
            <SignalIcon class="h-5 w-5 text-gray-400" />
            <span class="text-sm text-gray-500 dark:text-gray-400">Error Rate</span>
          </div>
          <p
            class="mt-1 text-xl font-semibold"
            :class="getMetricColor(health.metrics.errorRate, { warning: 1, critical: 5 })"
          >
            {{ health.metrics.errorRate.toFixed(2) }}%
          </p>
        </div>
      </div>

      <!-- Components -->
      <div class="mt-4">
        <h4 class="text-sm font-medium text-gray-700 dark:text-gray-300">Components</h4>
        <div class="mt-2 space-y-2">
          <div
            v-for="component in health.components"
            :key="component.name"
            class="flex items-center justify-between rounded-lg border p-2 dark:border-gray-700"
          >
            <div class="flex items-center gap-2">
              <CheckCircleIcon
                v-if="component.status === 'healthy'"
                class="h-4 w-4 text-green-500"
              />
              <ExclamationTriangleIcon
                v-else-if="component.status === 'degraded'"
                class="h-4 w-4 text-yellow-500"
              />
              <XCircleIcon v-else class="h-4 w-4 text-red-500" />
              <span class="text-sm font-medium text-gray-900 dark:text-white">
                {{ component.name }}
              </span>
            </div>
            <div class="flex items-center gap-2">
              <span
                v-if="component.responseTime"
                class="text-xs text-gray-500 dark:text-gray-400"
              >
                {{ component.responseTime }}ms
              </span>
              <span
                class="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium"
                :class="getComponentStatusClasses(component.status)"
              >
                {{ component.status }}
              </span>
            </div>
          </div>
        </div>
      </div>

      <!-- Additional Metrics -->
      <div class="mt-4 grid grid-cols-3 gap-4 border-t pt-4 dark:border-gray-700">
        <div class="text-center">
          <p class="text-2xl font-bold text-gray-900 dark:text-white">
            {{ health.metrics.activeConnections }}
          </p>
          <p class="text-xs text-gray-500 dark:text-gray-400">Active Connections</p>
        </div>
        <div class="text-center">
          <p class="text-2xl font-bold text-gray-900 dark:text-white">
            {{ health.metrics.requestsPerMinute }}
          </p>
          <p class="text-xs text-gray-500 dark:text-gray-400">Req/min</p>
        </div>
        <div class="text-center">
          <p class="text-2xl font-bold text-gray-900 dark:text-white">
            {{ health.metrics.averageResponseTime }}ms
          </p>
          <p class="text-xs text-gray-500 dark:text-gray-400">Avg Response</p>
        </div>
      </div>
    </template>
  </div>
</template>
