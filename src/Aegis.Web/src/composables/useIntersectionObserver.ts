import { ref, onMounted, onUnmounted, watch, type Ref } from 'vue'

export interface UseIntersectionObserverOptions {
  /** Root element for intersection (default: viewport) */
  root?: Element | Document | null
  /** Margin around root */
  rootMargin?: string
  /** Threshold(s) at which to trigger callback */
  threshold?: number | number[]
  /** Only trigger once then stop observing */
  once?: boolean
}

export interface UseIntersectionObserverReturn {
  /** Whether the element is currently intersecting */
  isIntersecting: Ref<boolean>
  /** The intersection ratio (0-1) */
  intersectionRatio: Ref<number>
  /** Stop observing the element */
  stop: () => void
  /** Start observing the element */
  start: () => void
}

/**
 * Composable for observing element intersection with viewport
 * Useful for lazy loading, infinite scroll, and visibility detection
 */
export function useIntersectionObserver(
  target: Ref<Element | null | undefined>,
  callback?: (entry: IntersectionObserverEntry) => void,
  options: UseIntersectionObserverOptions = {}
): UseIntersectionObserverReturn {
  const {
    root = null,
    rootMargin = '0px',
    threshold = 0,
    once = false
  } = options

  const isIntersecting = ref(false)
  const intersectionRatio = ref(0)

  let observer: IntersectionObserver | null = null
  let isActive = true

  function handleIntersection(entries: IntersectionObserverEntry[]) {
    const entry = entries[0]
    if (!entry) return

    isIntersecting.value = entry.isIntersecting
    intersectionRatio.value = entry.intersectionRatio

    callback?.(entry)

    // Stop observing if once is true and element is visible
    if (once && entry.isIntersecting) {
      stop()
    }
  }

  function createObserver() {
    if (observer) {
      observer.disconnect()
    }

    observer = new IntersectionObserver(handleIntersection, {
      root,
      rootMargin,
      threshold
    })

    if (target.value && isActive) {
      observer.observe(target.value)
    }
  }

  function stop() {
    isActive = false
    if (observer) {
      observer.disconnect()
      observer = null
    }
  }

  function start() {
    isActive = true
    createObserver()
  }

  // Watch target changes
  watch(target, (newTarget, oldTarget) => {
    if (observer) {
      if (oldTarget) {
        observer.unobserve(oldTarget)
      }
      if (newTarget && isActive) {
        observer.observe(newTarget)
      }
    }
  })

  onMounted(() => {
    createObserver()
  })

  onUnmounted(() => {
    stop()
  })

  return {
    isIntersecting,
    intersectionRatio,
    stop,
    start
  }
}

/**
 * Composable for lazy loading content when it enters the viewport
 */
export function useLazyLoad(
  target: Ref<Element | null | undefined>,
  options: UseIntersectionObserverOptions = {}
) {
  const isLoaded = ref(false)
  const isVisible = ref(false)

  const { isIntersecting, stop } = useIntersectionObserver(
    target,
    (entry) => {
      if (entry.isIntersecting && !isLoaded.value) {
        isLoaded.value = true
        isVisible.value = true
        stop()
      }
    },
    {
      ...options,
      once: true,
      rootMargin: options.rootMargin ?? '100px' // Pre-load slightly before visible
    }
  )

  return {
    isLoaded,
    isVisible,
    isIntersecting
  }
}

/**
 * Composable for infinite scroll detection
 */
export function useInfiniteScroll(
  target: Ref<Element | null | undefined>,
  callback: () => void | Promise<void>,
  options: UseIntersectionObserverOptions & {
    /** Disable infinite scroll */
    disabled?: boolean
    /** Distance from bottom to trigger (rootMargin) */
    distance?: string
  } = {}
) {
  const {
    disabled = false,
    distance = '200px',
    ...observerOptions
  } = options

  const isLoading = ref(false)

  const { isIntersecting, stop, start } = useIntersectionObserver(
    target,
    async (entry) => {
      if (entry.isIntersecting && !isLoading.value && !disabled) {
        isLoading.value = true
        try {
          await callback()
        } finally {
          isLoading.value = false
        }
      }
    },
    {
      ...observerOptions,
      rootMargin: distance
    }
  )

  // Watch disabled state
  watch(() => disabled, (isDisabled) => {
    if (isDisabled) {
      stop()
    } else {
      start()
    }
  })

  return {
    isIntersecting,
    isLoading,
    stop,
    start
  }
}

export default useIntersectionObserver
