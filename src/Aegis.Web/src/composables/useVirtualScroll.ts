import { ref, computed, onMounted, onUnmounted, watch, type Ref } from 'vue'

export interface VirtualScrollOptions {
  /** Total number of items */
  itemCount: number
  /** Height of each item in pixels */
  itemHeight: number
  /** Container height in pixels (or 'auto' to use viewport) */
  containerHeight?: number | 'auto'
  /** Number of items to render outside visible area (buffer) */
  overscan?: number
  /** Estimated item height for variable height items */
  estimatedItemHeight?: number
}

export interface VirtualScrollReturn {
  /** Ref to attach to the scroll container */
  containerRef: Ref<HTMLElement | null>
  /** Start index of visible items */
  startIndex: Ref<number>
  /** End index of visible items */
  endIndex: Ref<number>
  /** Visible items to render */
  visibleItems: Ref<number[]>
  /** Total height of all items (for scroll area) */
  totalHeight: Ref<number>
  /** Offset for the first visible item */
  offsetTop: Ref<number>
  /** Scroll to a specific index */
  scrollToIndex: (index: number, behavior?: ScrollBehavior) => void
  /** Scroll to top */
  scrollToTop: (behavior?: ScrollBehavior) => void
  /** Scroll to bottom */
  scrollToBottom: (behavior?: ScrollBehavior) => void
  /** Current scroll position */
  scrollTop: Ref<number>
  /** Is currently scrolling */
  isScrolling: Ref<boolean>
}

/**
 * Composable for virtual scrolling of large lists
 * Only renders items that are visible in the viewport
 */
export function useVirtualScroll(options: Ref<VirtualScrollOptions> | VirtualScrollOptions): VirtualScrollReturn {
  const containerRef = ref<HTMLElement | null>(null)
  const scrollTop = ref(0)
  const containerHeight = ref(0)
  const isScrolling = ref(false)

  let scrollingTimeout: ReturnType<typeof setTimeout> | null = null

  // Normalize options to reactive
  const opts = computed(() => {
    const o = 'value' in options ? options.value : options
    return {
      itemCount: o.itemCount,
      itemHeight: o.itemHeight,
      containerHeight: o.containerHeight ?? 'auto',
      overscan: o.overscan ?? 3,
      estimatedItemHeight: o.estimatedItemHeight ?? o.itemHeight
    }
  })

  // Total height of all items
  const totalHeight = computed(() => opts.value.itemCount * opts.value.itemHeight)

  // Calculate visible range
  const startIndex = computed(() => {
    const start = Math.floor(scrollTop.value / opts.value.itemHeight)
    return Math.max(0, start - opts.value.overscan)
  })

  const endIndex = computed(() => {
    const visibleCount = Math.ceil(containerHeight.value / opts.value.itemHeight)
    const end = Math.floor(scrollTop.value / opts.value.itemHeight) + visibleCount
    return Math.min(opts.value.itemCount - 1, end + opts.value.overscan)
  })

  // Array of visible item indices
  const visibleItems = computed(() => {
    const items: number[] = []
    for (let i = startIndex.value; i <= endIndex.value; i++) {
      items.push(i)
    }
    return items
  })

  // Offset for positioning visible items
  const offsetTop = computed(() => startIndex.value * opts.value.itemHeight)

  // Handle scroll events
  function onScroll(event: Event) {
    const target = event.target as HTMLElement
    scrollTop.value = target.scrollTop
    isScrolling.value = true

    // Clear previous timeout
    if (scrollingTimeout) {
      clearTimeout(scrollingTimeout)
    }

    // Set scrolling to false after scroll ends
    scrollingTimeout = setTimeout(() => {
      isScrolling.value = false
    }, 150)
  }

  // Update container height
  function updateContainerHeight() {
    if (containerRef.value) {
      if (opts.value.containerHeight === 'auto') {
        containerHeight.value = containerRef.value.clientHeight
      } else {
        containerHeight.value = opts.value.containerHeight
      }
    }
  }

  // Scroll to specific index
  function scrollToIndex(index: number, behavior: ScrollBehavior = 'auto') {
    if (!containerRef.value) return

    const targetScrollTop = index * opts.value.itemHeight
    containerRef.value.scrollTo({
      top: targetScrollTop,
      behavior
    })
  }

  // Scroll to top
  function scrollToTop(behavior: ScrollBehavior = 'auto') {
    scrollToIndex(0, behavior)
  }

  // Scroll to bottom
  function scrollToBottom(behavior: ScrollBehavior = 'auto') {
    scrollToIndex(opts.value.itemCount - 1, behavior)
  }

  // Setup and cleanup
  onMounted(() => {
    if (containerRef.value) {
      containerRef.value.addEventListener('scroll', onScroll, { passive: true })
      updateContainerHeight()

      // Watch for resize
      const resizeObserver = new ResizeObserver(() => {
        updateContainerHeight()
      })
      resizeObserver.observe(containerRef.value)

      onUnmounted(() => {
        if (containerRef.value) {
          containerRef.value.removeEventListener('scroll', onScroll)
          resizeObserver.disconnect()
        }
        if (scrollingTimeout) {
          clearTimeout(scrollingTimeout)
        }
      })
    }
  })

  // Watch for container ref changes
  watch(containerRef, (newContainer, oldContainer) => {
    if (oldContainer) {
      oldContainer.removeEventListener('scroll', onScroll)
    }
    if (newContainer) {
      newContainer.addEventListener('scroll', onScroll, { passive: true })
      updateContainerHeight()
    }
  })

  return {
    containerRef,
    startIndex,
    endIndex,
    visibleItems,
    totalHeight,
    offsetTop,
    scrollToIndex,
    scrollToTop,
    scrollToBottom,
    scrollTop,
    isScrolling
  }
}

/**
 * Composable for variable height virtual scrolling
 * Uses estimated heights and measures actual heights
 */
export function useVariableVirtualScroll<T>(
  items: Ref<T[]>,
  estimatedHeight: number = 50
) {
  const containerRef = ref<HTMLElement | null>(null)
  const scrollTop = ref(0)
  const containerHeight = ref(0)
  const measuredHeights = ref<Map<number, number>>(new Map())
  const isScrolling = ref(false)

  let scrollingTimeout: ReturnType<typeof setTimeout> | null = null

  // Get height for an item (measured or estimated)
  function getItemHeight(index: number): number {
    return measuredHeights.value.get(index) ?? estimatedHeight
  }

  // Calculate item positions
  const itemPositions = computed(() => {
    const positions: { top: number; height: number }[] = []
    let top = 0

    for (let i = 0; i < items.value.length; i++) {
      const height = getItemHeight(i)
      positions.push({ top, height })
      top += height
    }

    return positions
  })

  // Total height
  const totalHeight = computed(() => {
    if (itemPositions.value.length === 0) return 0
    const last = itemPositions.value[itemPositions.value.length - 1]
    return last.top + last.height
  })

  // Find visible range using binary search
  const visibleRange = computed(() => {
    const positions = itemPositions.value
    if (positions.length === 0) return { start: 0, end: 0 }

    // Binary search for start index
    let start = 0
    let end = positions.length - 1
    const viewportTop = scrollTop.value
    const viewportBottom = scrollTop.value + containerHeight.value

    // Find first visible item
    while (start < end) {
      const mid = Math.floor((start + end) / 2)
      if (positions[mid].top + positions[mid].height < viewportTop) {
        start = mid + 1
      } else {
        end = mid
      }
    }

    const startIndex = Math.max(0, start - 3) // overscan

    // Find last visible item
    let endIndex = startIndex
    while (endIndex < positions.length && positions[endIndex].top < viewportBottom) {
      endIndex++
    }
    endIndex = Math.min(positions.length - 1, endIndex + 3) // overscan

    return { start: startIndex, end: endIndex }
  })

  // Visible items
  const visibleItems = computed(() => {
    const { start, end } = visibleRange.value
    return items.value.slice(start, end + 1).map((item, i) => ({
      item,
      index: start + i,
      style: {
        position: 'absolute' as const,
        top: `${itemPositions.value[start + i]?.top ?? 0}px`,
        left: 0,
        right: 0
      }
    }))
  })

  // Measure an item's height
  function measureItem(index: number, height: number) {
    if (measuredHeights.value.get(index) !== height) {
      measuredHeights.value.set(index, height)
    }
  }

  // Handle scroll
  function onScroll(event: Event) {
    const target = event.target as HTMLElement
    scrollTop.value = target.scrollTop
    isScrolling.value = true

    if (scrollingTimeout) clearTimeout(scrollingTimeout)
    scrollingTimeout = setTimeout(() => {
      isScrolling.value = false
    }, 150)
  }

  // Update container height
  function updateContainerHeight() {
    if (containerRef.value) {
      containerHeight.value = containerRef.value.clientHeight
    }
  }

  // Scroll to index
  function scrollToIndex(index: number, behavior: ScrollBehavior = 'auto') {
    if (!containerRef.value || !itemPositions.value[index]) return
    containerRef.value.scrollTo({
      top: itemPositions.value[index].top,
      behavior
    })
  }

  onMounted(() => {
    if (containerRef.value) {
      containerRef.value.addEventListener('scroll', onScroll, { passive: true })
      updateContainerHeight()

      const resizeObserver = new ResizeObserver(updateContainerHeight)
      resizeObserver.observe(containerRef.value)

      onUnmounted(() => {
        if (containerRef.value) {
          containerRef.value.removeEventListener('scroll', onScroll)
          resizeObserver.disconnect()
        }
        if (scrollingTimeout) clearTimeout(scrollingTimeout)
      })
    }
  })

  return {
    containerRef,
    visibleItems,
    totalHeight,
    scrollTop,
    isScrolling,
    measureItem,
    scrollToIndex
  }
}

export default useVirtualScroll
