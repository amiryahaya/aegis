<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(defineProps<{
  /** Type of skeleton */
  type?: 'text' | 'circle' | 'rect' | 'card' | 'avatar' | 'button' | 'input'
  /** Width of the skeleton */
  width?: number | string
  /** Height of the skeleton */
  height?: number | string
  /** Number of lines (for text type) */
  lines?: number
  /** Animation type */
  animation?: 'pulse' | 'wave' | 'none'
  /** Border radius */
  rounded?: 'none' | 'sm' | 'md' | 'lg' | 'full'
  /** Show skeleton or content */
  loading?: boolean
}>(), {
  type: 'text',
  lines: 1,
  animation: 'pulse',
  rounded: 'md',
  loading: true
})

// Skeleton classes based on type
const typeClasses = computed(() => {
  switch (props.type) {
    case 'circle':
    case 'avatar':
      return 'rounded-full'
    case 'button':
      return 'rounded-lg'
    case 'input':
      return 'rounded-md'
    case 'card':
      return 'rounded-xl'
    default:
      return `rounded-${props.rounded}`
  }
})

// Default dimensions based on type
const defaultDimensions = computed(() => {
  switch (props.type) {
    case 'circle':
      return { width: '40px', height: '40px' }
    case 'avatar':
      return { width: '48px', height: '48px' }
    case 'button':
      return { width: '100px', height: '36px' }
    case 'input':
      return { width: '100%', height: '40px' }
    case 'card':
      return { width: '100%', height: '200px' }
    case 'text':
      return { width: '100%', height: '16px' }
    default:
      return { width: '100%', height: '24px' }
  }
})

// Computed style
const style = computed(() => ({
  width: props.width
    ? (typeof props.width === 'number' ? `${props.width}px` : props.width)
    : defaultDimensions.value.width,
  height: props.height
    ? (typeof props.height === 'number' ? `${props.height}px` : props.height)
    : defaultDimensions.value.height
}))

// Animation class
const animationClass = computed(() => {
  switch (props.animation) {
    case 'pulse':
      return 'animate-pulse'
    case 'wave':
      return 'skeleton-wave'
    default:
      return ''
  }
})

// Generate varying widths for text lines
function getLineWidth(index: number, total: number): string {
  if (index === total - 1 && total > 1) {
    return '60%' // Last line is shorter
  }
  // Slight variation for other lines
  const variation = (index % 3) * 5
  return `${95 - variation}%`
}
</script>

<template>
  <template v-if="loading">
    <!-- Text skeleton with multiple lines -->
    <div v-if="type === 'text' && lines > 1" class="space-y-2">
      <div
        v-for="i in lines"
        :key="i"
        :class="['bg-gray-200 dark:bg-gray-700', animationClass, typeClasses]"
        :style="{
          width: getLineWidth(i - 1, lines),
          height: style.height
        }"
      />
    </div>

    <!-- Single skeleton element -->
    <div
      v-else
      :class="['bg-gray-200 dark:bg-gray-700', animationClass, typeClasses]"
      :style="style"
    />
  </template>

  <!-- Content when not loading -->
  <slot v-else />
</template>

<style scoped>
.skeleton-wave {
  position: relative;
  overflow: hidden;
}

.skeleton-wave::after {
  content: '';
  position: absolute;
  inset: 0;
  transform: translateX(-100%);
  background: linear-gradient(
    90deg,
    transparent,
    rgba(255, 255, 255, 0.2),
    transparent
  );
  animation: wave 1.5s infinite;
}

@keyframes wave {
  100% {
    transform: translateX(100%);
  }
}

/* Dark mode wave */
.dark .skeleton-wave::after {
  background: linear-gradient(
    90deg,
    transparent,
    rgba(255, 255, 255, 0.05),
    transparent
  );
}
</style>
