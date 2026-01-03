<script setup lang="ts">
import { computed } from 'vue'
import {
  ArrowTrendingUpIcon,
  ArrowTrendingDownIcon,
  MinusIcon
} from '@heroicons/vue/24/outline'

const props = withDefaults(defineProps<{
  /** Title of the stat */
  title: string
  /** Current value */
  value: number | string
  /** Previous period value for comparison */
  previousValue?: number
  /** Format type */
  format?: 'number' | 'percent' | 'currency' | 'duration' | 'bytes'
  /** Prefix for the value */
  prefix?: string
  /** Suffix for the value */
  suffix?: string
  /** Icon component */
  icon?: object
  /** Icon background color */
  iconColor?: string
  /** Whether increase is positive (default true) */
  increaseIsPositive?: boolean
  /** Loading state */
  loading?: boolean
}>(), {
  format: 'number',
  increaseIsPositive: true,
  loading: false
})

// Format the value based on type
const formattedValue = computed(() => {
  if (typeof props.value === 'string') return props.value

  switch (props.format) {
    case 'percent':
      return `${props.value.toFixed(1)}%`
    case 'currency':
      return new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: 'USD',
        minimumFractionDigits: 0,
        maximumFractionDigits: 0
      }).format(props.value)
    case 'duration':
      if (props.value < 1) return `${(props.value * 1000).toFixed(0)}ms`
      return `${props.value.toFixed(2)}s`
    case 'bytes':
      if (props.value < 1024) return `${props.value}B`
      if (props.value < 1024 * 1024) return `${(props.value / 1024).toFixed(1)}KB`
      if (props.value < 1024 * 1024 * 1024) return `${(props.value / 1024 / 1024).toFixed(1)}MB`
      return `${(props.value / 1024 / 1024 / 1024).toFixed(2)}GB`
    default:
      if (props.value >= 1000000) return `${(props.value / 1000000).toFixed(1)}M`
      if (props.value >= 1000) return `${(props.value / 1000).toFixed(1)}K`
      return props.value.toLocaleString()
  }
})

// Calculate change percentage
const change = computed(() => {
  if (!props.previousValue || typeof props.value !== 'number') return null

  const diff = props.value - props.previousValue
  const percent = props.previousValue !== 0 ? (diff / props.previousValue) * 100 : 0

  return {
    value: diff,
    percent,
    isPositive: diff > 0,
    isNeutral: diff === 0
  }
})

// Determine if change is good or bad
const changeColor = computed(() => {
  if (!change.value || change.value.isNeutral) return 'text-gray-500'

  const isGood = props.increaseIsPositive ? change.value.isPositive : !change.value.isPositive
  return isGood ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'
})

const changeBgColor = computed(() => {
  if (!change.value || change.value.isNeutral) return 'bg-gray-100 dark:bg-gray-700'

  const isGood = props.increaseIsPositive ? change.value.isPositive : !change.value.isPositive
  return isGood
    ? 'bg-green-100 dark:bg-green-900/30'
    : 'bg-red-100 dark:bg-red-900/30'
})
</script>

<template>
  <div class="card p-4 sm:p-6">
    <!-- Loading state -->
    <div v-if="loading" class="animate-pulse">
      <div class="flex items-center justify-between">
        <div class="h-4 w-24 rounded bg-gray-200 dark:bg-gray-700" />
        <div class="h-10 w-10 rounded-lg bg-gray-200 dark:bg-gray-700" />
      </div>
      <div class="mt-4 h-8 w-32 rounded bg-gray-200 dark:bg-gray-700" />
      <div class="mt-2 h-5 w-20 rounded bg-gray-200 dark:bg-gray-700" />
    </div>

    <!-- Content -->
    <div v-else>
      <div class="flex items-center justify-between">
        <p class="text-sm font-medium text-gray-500 dark:text-gray-400">
          {{ title }}
        </p>
        <div
          v-if="icon"
          class="flex h-10 w-10 items-center justify-center rounded-lg"
          :class="iconColor || 'bg-aegis-100 dark:bg-aegis-900/30'"
        >
          <component
            :is="icon"
            class="h-5 w-5"
            :class="iconColor?.includes('bg-') ? 'text-white' : 'text-aegis-600 dark:text-aegis-400'"
          />
        </div>
      </div>

      <div class="mt-4 flex items-baseline gap-2">
        <span v-if="prefix" class="text-lg text-gray-500 dark:text-gray-400">
          {{ prefix }}
        </span>
        <span class="text-2xl font-bold text-gray-900 dark:text-white">
          {{ formattedValue }}
        </span>
        <span v-if="suffix" class="text-lg text-gray-500 dark:text-gray-400">
          {{ suffix }}
        </span>
      </div>

      <!-- Change indicator -->
      <div v-if="change" class="mt-2 flex items-center gap-1.5">
        <span
          class="inline-flex items-center gap-0.5 rounded-full px-2 py-0.5 text-xs font-medium"
          :class="[changeBgColor, changeColor]"
        >
          <ArrowTrendingUpIcon v-if="change.isPositive" class="h-3 w-3" />
          <ArrowTrendingDownIcon v-else-if="!change.isNeutral" class="h-3 w-3" />
          <MinusIcon v-else class="h-3 w-3" />
          {{ Math.abs(change.percent).toFixed(1) }}%
        </span>
        <span class="text-xs text-gray-500 dark:text-gray-400">
          vs previous period
        </span>
      </div>
    </div>
  </div>
</template>
