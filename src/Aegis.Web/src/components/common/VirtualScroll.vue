<script setup lang="ts">
import { computed } from 'vue'
import { useVirtualScroll } from '@/composables/useVirtualScroll'

const props = withDefaults(defineProps<{
  /** Total number of items */
  itemCount: number
  /** Height of each item in pixels */
  itemHeight: number
  /** Container height in pixels (optional, defaults to 100%) */
  height?: number | string
  /** Number of items to render outside visible area */
  overscan?: number
  /** CSS class for the container */
  containerClass?: string
}>(), {
  overscan: 3,
  containerClass: ''
})

const emit = defineEmits<{
  (e: 'scroll', scrollTop: number): void
  (e: 'scrollEnd'): void
}>()

// Virtual scroll options
const options = computed(() => ({
  itemCount: props.itemCount,
  itemHeight: props.itemHeight,
  containerHeight: typeof props.height === 'number' ? props.height : 'auto' as const,
  overscan: props.overscan
}))

const {
  containerRef,
  visibleItems,
  totalHeight,
  offsetTop,
  scrollTop,
  isScrolling,
  scrollToIndex,
  scrollToTop,
  scrollToBottom
} = useVirtualScroll(options)

// Container style
const containerStyle = computed(() => ({
  height: typeof props.height === 'string' ? props.height : `${props.height}px`,
  overflow: 'auto',
  position: 'relative' as const
}))

// Inner container style (for total scroll height)
const innerStyle = computed(() => ({
  height: `${totalHeight.value}px`,
  position: 'relative' as const
}))

// Visible items wrapper style
const itemsStyle = computed(() => ({
  position: 'absolute' as const,
  top: `${offsetTop.value}px`,
  left: 0,
  right: 0
}))

// Expose methods for parent components
defineExpose({
  scrollToIndex,
  scrollToTop,
  scrollToBottom,
  scrollTop,
  isScrolling
})
</script>

<template>
  <div
    ref="containerRef"
    :style="containerStyle"
    :class="['virtual-scroll-container', containerClass]"
    @scroll="emit('scroll', scrollTop)"
  >
    <div :style="innerStyle" class="virtual-scroll-inner">
      <div :style="itemsStyle" class="virtual-scroll-items">
        <template v-for="index in visibleItems" :key="index">
          <div
            class="virtual-scroll-item"
            :style="{ height: `${itemHeight}px` }"
          >
            <slot :index="index" :isScrolling="isScrolling">
              <!-- Default slot content -->
              <div class="p-4 border-b border-gray-200 dark:border-gray-700">
                Item {{ index }}
              </div>
            </slot>
          </div>
        </template>
      </div>
    </div>

    <!-- Empty state slot -->
    <slot v-if="itemCount === 0" name="empty">
      <div class="flex items-center justify-center h-full text-gray-500 dark:text-gray-400">
        No items to display
      </div>
    </slot>

    <!-- Loading state slot -->
    <slot name="loading" />
  </div>
</template>

<style scoped>
.virtual-scroll-container {
  will-change: scroll-position;
}

.virtual-scroll-inner {
  will-change: transform;
}

.virtual-scroll-item {
  box-sizing: border-box;
}
</style>
