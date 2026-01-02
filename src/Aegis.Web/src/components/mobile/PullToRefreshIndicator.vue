<script setup lang="ts">
import { computed } from 'vue'
import { ArrowPathIcon } from '@heroicons/vue/24/outline'

interface Props {
  pullDistance: number
  progress: number
  isRefreshing: boolean
  threshold?: number
}

const props = withDefaults(defineProps<Props>(), {
  threshold: 80
})

// Calculate rotation based on progress
const rotation = computed(() => {
  return props.progress * 360
})

// Calculate opacity based on progress
const opacity = computed(() => {
  return Math.min(props.progress * 1.5, 1)
})

// Transform for pull animation
const transform = computed(() => {
  const translateY = Math.min(props.pullDistance, props.threshold)
  return `translateY(${translateY}px)`
})
</script>

<template>
  <div
    class="absolute top-0 left-0 right-0 flex justify-center pointer-events-none z-10"
    :style="{ transform }"
  >
    <div
      class="flex items-center justify-center w-10 h-10 -mt-12 bg-white dark:bg-gray-800 rounded-full shadow-lg"
      :style="{ opacity }"
    >
      <ArrowPathIcon
        class="w-5 h-5 text-aegis-600 dark:text-aegis-400 transition-transform"
        :class="{ 'animate-spin': isRefreshing }"
        :style="{ transform: `rotate(${rotation}deg)` }"
      />
    </div>
  </div>
</template>
