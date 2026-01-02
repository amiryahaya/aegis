<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch, nextTick } from 'vue'
import { XMarkIcon, ChevronLeftIcon } from '@heroicons/vue/24/outline'
import type { TourStep, TourStepPlacement } from '@/types/onboarding'

interface Props {
  step: TourStep
  currentIndex: number
  totalSteps: number
  isFirst: boolean
  isLast: boolean
}

const props = defineProps<Props>()

const emit = defineEmits<{
  next: []
  previous: []
  skip: []
  close: []
}>()

const tooltipRef = ref<HTMLDivElement | null>(null)
const position = ref({ top: 0, left: 0 })
const arrowPosition = ref<TourStepPlacement>('top')

// Calculate position based on target element
function calculatePosition(): void {
  if (!props.step.target) {
    // Center in viewport if no target
    position.value = {
      top: window.innerHeight / 2,
      left: window.innerWidth / 2
    }
    arrowPosition.value = 'center'
    return
  }

  const target = document.querySelector(props.step.target) as HTMLElement
  if (!target) {
    console.warn(`Tour target not found: ${props.step.target}`)
    position.value = {
      top: window.innerHeight / 2,
      left: window.innerWidth / 2
    }
    arrowPosition.value = 'center'
    return
  }

  const targetRect = target.getBoundingClientRect()
  const tooltip = tooltipRef.value
  const tooltipRect = tooltip?.getBoundingClientRect() || { width: 320, height: 200 }
  const offset = 12

  const placement = props.step.placement || 'bottom'
  let top = 0
  let left = 0

  switch (placement) {
    case 'top':
      top = targetRect.top - tooltipRect.height - offset
      left = targetRect.left + (targetRect.width - tooltipRect.width) / 2
      arrowPosition.value = 'bottom'
      break
    case 'bottom':
      top = targetRect.bottom + offset
      left = targetRect.left + (targetRect.width - tooltipRect.width) / 2
      arrowPosition.value = 'top'
      break
    case 'left':
      top = targetRect.top + (targetRect.height - tooltipRect.height) / 2
      left = targetRect.left - tooltipRect.width - offset
      arrowPosition.value = 'right'
      break
    case 'right':
      top = targetRect.top + (targetRect.height - tooltipRect.height) / 2
      left = targetRect.right + offset
      arrowPosition.value = 'left'
      break
    case 'center':
      top = window.innerHeight / 2 - tooltipRect.height / 2
      left = window.innerWidth / 2 - tooltipRect.width / 2
      arrowPosition.value = 'center'
      break
  }

  // Keep within viewport bounds
  const margin = 16
  top = Math.max(margin, Math.min(top, window.innerHeight - tooltipRect.height - margin))
  left = Math.max(margin, Math.min(left, window.innerWidth - tooltipRect.width - margin))

  position.value = { top, left }

  // Scroll target into view if needed
  if (props.step.target && target) {
    const isInView = targetRect.top >= 0 &&
      targetRect.left >= 0 &&
      targetRect.bottom <= window.innerHeight &&
      targetRect.right <= window.innerWidth

    if (!isInView) {
      target.scrollIntoView({ behavior: 'smooth', block: 'center' })
    }
  }
}

// Arrow classes based on position
const arrowClasses = computed(() => {
  switch (arrowPosition.value) {
    case 'top':
      return 'bottom-full left-1/2 -translate-x-1/2 border-b-white dark:border-b-gray-800 border-t-transparent border-l-transparent border-r-transparent'
    case 'bottom':
      return 'top-full left-1/2 -translate-x-1/2 border-t-white dark:border-t-gray-800 border-b-transparent border-l-transparent border-r-transparent'
    case 'left':
      return 'right-full top-1/2 -translate-y-1/2 border-r-white dark:border-r-gray-800 border-l-transparent border-t-transparent border-b-transparent'
    case 'right':
      return 'left-full top-1/2 -translate-y-1/2 border-l-white dark:border-l-gray-800 border-r-transparent border-t-transparent border-b-transparent'
    default:
      return 'hidden'
  }
})

// Handle window resize
function handleResize(): void {
  calculatePosition()
}

// Watch for step changes
watch(() => props.step, async () => {
  await nextTick()
  calculatePosition()
}, { immediate: true })

onMounted(() => {
  window.addEventListener('resize', handleResize)
  window.addEventListener('scroll', handleResize)
  calculatePosition()
})

onUnmounted(() => {
  window.removeEventListener('resize', handleResize)
  window.removeEventListener('scroll', handleResize)
})
</script>

<template>
  <Teleport to="body">
    <!-- Spotlight overlay -->
    <div
      v-if="step.target"
      class="fixed inset-0 z-40 pointer-events-none"
    >
      <div class="absolute inset-0 bg-black/50 backdrop-blur-sm" />
    </div>

    <!-- Tooltip -->
    <div
      ref="tooltipRef"
      class="fixed z-50 w-80 bg-white dark:bg-gray-800 rounded-xl shadow-2xl border border-gray-200 dark:border-gray-700 transition-all duration-200"
      :style="{ top: `${position.top}px`, left: `${position.left}px` }"
    >
      <!-- Arrow -->
      <div
        v-if="arrowPosition !== 'center'"
        class="absolute w-0 h-0 border-8"
        :class="arrowClasses"
      />

      <!-- Close button -->
      <button
        @click="emit('close')"
        class="absolute top-2 right-2 p-1 text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700"
      >
        <XMarkIcon class="h-4 w-4" />
      </button>

      <!-- Content -->
      <div class="p-4">
        <h3 class="text-lg font-semibold text-gray-900 dark:text-white pr-6">
          {{ step.title }}
        </h3>
        <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
          {{ step.content }}
        </p>
      </div>

      <!-- Footer -->
      <div class="px-4 py-3 bg-gray-50 dark:bg-gray-700/50 rounded-b-xl border-t border-gray-200 dark:border-gray-700">
        <div class="flex items-center justify-between">
          <!-- Progress -->
          <div v-if="step.showProgress !== false" class="flex items-center gap-2">
            <span class="text-xs text-gray-500 dark:text-gray-400">
              {{ currentIndex + 1 }} of {{ totalSteps }}
            </span>
            <div class="flex gap-1">
              <div
                v-for="i in totalSteps"
                :key="i"
                class="w-1.5 h-1.5 rounded-full transition-colors"
                :class="[
                  i - 1 === currentIndex
                    ? 'bg-aegis-500'
                    : i - 1 < currentIndex
                      ? 'bg-aegis-300 dark:bg-aegis-700'
                      : 'bg-gray-300 dark:bg-gray-600'
                ]"
              />
            </div>
          </div>

          <!-- Skip link -->
          <button
            v-if="step.showSkip !== false"
            @click="emit('skip')"
            class="text-xs text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
          >
            Skip tour
          </button>

          <!-- Navigation buttons -->
          <div class="flex items-center gap-2">
            <button
              v-if="!isFirst"
              @click="emit('previous')"
              class="p-1.5 text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200 rounded-lg hover:bg-gray-200 dark:hover:bg-gray-600"
            >
              <ChevronLeftIcon class="h-4 w-4" />
            </button>

            <button
              @click="emit('next')"
              class="px-3 py-1.5 text-sm font-medium text-white bg-aegis-600 hover:bg-aegis-700 rounded-lg transition-colors"
            >
              {{ isLast ? 'Finish' : 'Next' }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </Teleport>
</template>
