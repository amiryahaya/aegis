<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useInfiniteScroll } from '@/composables/useIntersectionObserver'

const props = withDefaults(defineProps<{
  /** Loading state */
  loading?: boolean
  /** No more items to load */
  finished?: boolean
  /** Error state */
  error?: boolean
  /** Distance from bottom to trigger load (in px) */
  distance?: number
  /** Disable infinite scroll */
  disabled?: boolean
  /** Direction of scroll */
  direction?: 'down' | 'up'
  /** Immediate load on mount */
  immediate?: boolean
}>(), {
  loading: false,
  finished: false,
  error: false,
  distance: 200,
  disabled: false,
  direction: 'down',
  immediate: false
})

const emit = defineEmits<{
  (e: 'load'): void
  (e: 'retry'): void
}>()

const triggerRef = ref<HTMLElement | null>(null)
const containerRef = ref<HTMLElement | null>(null)

// Determine if loading should be disabled
const isDisabled = computed(() =>
  props.disabled || props.loading || props.finished || props.error
)

// Setup infinite scroll
const { isLoading: isIntersecting } = useInfiniteScroll(
  triggerRef,
  () => {
    if (!isDisabled.value) {
      emit('load')
    }
  },
  {
    disabled: isDisabled.value,
    distance: `${props.distance}px`
  }
)

// Watch for disabled changes
watch(isDisabled, (disabled) => {
  // When re-enabled, trigger load if already intersecting
  if (!disabled && isIntersecting.value) {
    emit('load')
  }
})

// Retry loading
function retry() {
  emit('retry')
}

// Immediate load on mount
if (props.immediate) {
  emit('load')
}
</script>

<template>
  <div ref="containerRef" class="infinite-scroll-container">
    <!-- Content slot -->
    <slot />

    <!-- Trigger element -->
    <div
      ref="triggerRef"
      class="infinite-scroll-trigger"
      :class="{ 'order-first': direction === 'up' }"
    >
      <!-- Loading state -->
      <slot v-if="loading" name="loading">
        <div class="flex items-center justify-center py-4">
          <svg
            class="h-6 w-6 animate-spin text-aegis-500"
            fill="none"
            viewBox="0 0 24 24"
          >
            <circle
              class="opacity-25"
              cx="12"
              cy="12"
              r="10"
              stroke="currentColor"
              stroke-width="4"
            />
            <path
              class="opacity-75"
              fill="currentColor"
              d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
            />
          </svg>
          <span class="ml-2 text-sm text-gray-500 dark:text-gray-400">
            Loading more...
          </span>
        </div>
      </slot>

      <!-- Error state -->
      <slot v-else-if="error" name="error">
        <div class="flex flex-col items-center justify-center py-4">
          <p class="text-sm text-red-500 dark:text-red-400">
            Failed to load more items
          </p>
          <button
            class="mt-2 text-sm text-aegis-600 hover:text-aegis-700 dark:text-aegis-400"
            @click="retry"
          >
            Tap to retry
          </button>
        </div>
      </slot>

      <!-- Finished state -->
      <slot v-else-if="finished" name="finished">
        <div class="flex items-center justify-center py-4">
          <span class="text-sm text-gray-400 dark:text-gray-500">
            No more items
          </span>
        </div>
      </slot>

      <!-- Default trigger (invisible) -->
      <div v-else class="h-1" />
    </div>
  </div>
</template>

<style scoped>
.infinite-scroll-container {
  display: flex;
  flex-direction: column;
}

.infinite-scroll-trigger {
  flex-shrink: 0;
}
</style>
