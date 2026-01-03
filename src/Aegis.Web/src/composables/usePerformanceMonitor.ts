import { ref, onMounted, onUnmounted, computed, type Ref } from 'vue'

export interface PerformanceMetrics {
  /** Frames per second */
  fps: number
  /** Memory usage in MB (if available) */
  memoryUsage: number | null
  /** Time since last frame in ms */
  frameTime: number
  /** Long tasks detected (>50ms) */
  longTasks: number
  /** Cumulative Layout Shift score */
  cls: number
  /** Largest Contentful Paint in ms */
  lcp: number | null
  /** First Input Delay in ms */
  fid: number | null
  /** Time to Interactive in ms */
  tti: number | null
}

export interface UsePerformanceMonitorOptions {
  /** Enable FPS monitoring */
  trackFps?: boolean
  /** Enable memory monitoring */
  trackMemory?: boolean
  /** Enable long task monitoring */
  trackLongTasks?: boolean
  /** Enable Core Web Vitals monitoring */
  trackWebVitals?: boolean
  /** FPS sample interval in ms */
  fpsInterval?: number
}

export interface UsePerformanceMonitorReturn {
  /** Current performance metrics */
  metrics: Ref<PerformanceMetrics>
  /** Whether monitoring is active */
  isMonitoring: Ref<boolean>
  /** Start monitoring */
  start: () => void
  /** Stop monitoring */
  stop: () => void
  /** Reset metrics */
  reset: () => void
  /** Performance score (0-100) */
  score: Ref<number>
  /** Performance grade (A-F) */
  grade: Ref<string>
}

/**
 * Composable for monitoring application performance
 */
export function usePerformanceMonitor(
  options: UsePerformanceMonitorOptions = {}
): UsePerformanceMonitorReturn {
  const {
    trackFps = true,
    trackMemory = true,
    trackLongTasks = true,
    trackWebVitals = true,
    fpsInterval = 1000
  } = options

  const metrics = ref<PerformanceMetrics>({
    fps: 60,
    memoryUsage: null,
    frameTime: 16.67,
    longTasks: 0,
    cls: 0,
    lcp: null,
    fid: null,
    tti: null
  })

  const isMonitoring = ref(false)

  let animationFrameId: number | null = null
  let fpsIntervalId: ReturnType<typeof setInterval> | null = null
  let longTaskObserver: PerformanceObserver | null = null
  let clsObserver: PerformanceObserver | null = null
  let lcpObserver: PerformanceObserver | null = null

  // FPS tracking
  let frameCount = 0
  let lastFrameTime = performance.now()

  function trackFrame() {
    if (!isMonitoring.value) return

    const now = performance.now()
    const delta = now - lastFrameTime
    lastFrameTime = now
    frameCount++

    metrics.value.frameTime = delta

    animationFrameId = requestAnimationFrame(trackFrame)
  }

  function calculateFps() {
    metrics.value.fps = Math.round((frameCount * 1000) / fpsInterval)
    frameCount = 0
  }

  // Memory tracking
  function updateMemory() {
    if ('memory' in performance) {
      const memory = (performance as unknown as { memory: { usedJSHeapSize: number } }).memory
      metrics.value.memoryUsage = Math.round(memory.usedJSHeapSize / 1024 / 1024)
    }
  }

  // Long task tracking
  function setupLongTaskObserver() {
    if (!trackLongTasks || !('PerformanceObserver' in window)) return

    try {
      longTaskObserver = new PerformanceObserver((list) => {
        for (const entry of list.getEntries()) {
          if (entry.duration > 50) {
            metrics.value.longTasks++
          }
        }
      })
      longTaskObserver.observe({ entryTypes: ['longtask'] })
    } catch {
      // Long task observation not supported
    }
  }

  // CLS tracking
  function setupClsObserver() {
    if (!trackWebVitals || !('PerformanceObserver' in window)) return

    try {
      clsObserver = new PerformanceObserver((list) => {
        for (const entry of list.getEntries()) {
          const layoutShiftEntry = entry as PerformanceEntry & { hadRecentInput?: boolean; value?: number }
          if (!layoutShiftEntry.hadRecentInput && layoutShiftEntry.value) {
            metrics.value.cls += layoutShiftEntry.value
          }
        }
      })
      clsObserver.observe({ entryTypes: ['layout-shift'], buffered: true })
    } catch {
      // Layout shift observation not supported
    }
  }

  // LCP tracking
  function setupLcpObserver() {
    if (!trackWebVitals || !('PerformanceObserver' in window)) return

    try {
      lcpObserver = new PerformanceObserver((list) => {
        const entries = list.getEntries()
        const lastEntry = entries[entries.length - 1]
        if (lastEntry) {
          metrics.value.lcp = Math.round(lastEntry.startTime)
        }
      })
      lcpObserver.observe({ entryTypes: ['largest-contentful-paint'], buffered: true })
    } catch {
      // LCP observation not supported
    }
  }

  // FID tracking
  function setupFidTracking() {
    if (!trackWebVitals || !('PerformanceObserver' in window)) return

    try {
      const fidObserver = new PerformanceObserver((list) => {
        const entries = list.getEntries()
        const firstEntry = entries[0] as PerformanceEntry & { processingStart?: number }
        if (firstEntry && firstEntry.processingStart) {
          metrics.value.fid = Math.round(firstEntry.processingStart - firstEntry.startTime)
          fidObserver.disconnect()
        }
      })
      fidObserver.observe({ entryTypes: ['first-input'], buffered: true })
    } catch {
      // FID observation not supported
    }
  }

  // Calculate performance score
  const score = computed(() => {
    let totalScore = 100

    // FPS score (60 fps = 100%, below 30 = 0%)
    const fpsScore = Math.min(100, Math.max(0, ((metrics.value.fps - 30) / 30) * 100))
    totalScore -= (100 - fpsScore) * 0.3

    // CLS score (0 = 100%, >0.25 = 0%)
    const clsScore = Math.max(0, 100 - (metrics.value.cls * 400))
    totalScore -= (100 - clsScore) * 0.25

    // LCP score (<2.5s = 100%, >4s = 0%)
    if (metrics.value.lcp !== null) {
      const lcpScore = Math.max(0, Math.min(100, ((4000 - metrics.value.lcp) / 1500) * 100))
      totalScore -= (100 - lcpScore) * 0.25
    }

    // Long tasks penalty
    totalScore -= Math.min(20, metrics.value.longTasks * 2)

    return Math.round(Math.max(0, Math.min(100, totalScore)))
  })

  // Performance grade
  const grade = computed(() => {
    const s = score.value
    if (s >= 90) return 'A'
    if (s >= 80) return 'B'
    if (s >= 70) return 'C'
    if (s >= 60) return 'D'
    return 'F'
  })

  function start() {
    if (isMonitoring.value) return

    isMonitoring.value = true
    lastFrameTime = performance.now()
    frameCount = 0

    // Start FPS tracking
    if (trackFps) {
      animationFrameId = requestAnimationFrame(trackFrame)
      fpsIntervalId = setInterval(calculateFps, fpsInterval)
    }

    // Start memory tracking
    if (trackMemory) {
      updateMemory()
      setInterval(updateMemory, 5000)
    }

    // Setup observers
    setupLongTaskObserver()
    setupClsObserver()
    setupLcpObserver()
    setupFidTracking()
  }

  function stop() {
    isMonitoring.value = false

    if (animationFrameId) {
      cancelAnimationFrame(animationFrameId)
      animationFrameId = null
    }

    if (fpsIntervalId) {
      clearInterval(fpsIntervalId)
      fpsIntervalId = null
    }

    longTaskObserver?.disconnect()
    clsObserver?.disconnect()
    lcpObserver?.disconnect()
  }

  function reset() {
    metrics.value = {
      fps: 60,
      memoryUsage: null,
      frameTime: 16.67,
      longTasks: 0,
      cls: 0,
      lcp: null,
      fid: null,
      tti: null
    }
  }

  onMounted(() => {
    start()
  })

  onUnmounted(() => {
    stop()
  })

  return {
    metrics,
    isMonitoring,
    start,
    stop,
    reset,
    score,
    grade
  }
}

/**
 * Measure execution time of a function
 */
export function measureTime<T>(fn: () => T, label?: string): T {
  const start = performance.now()
  const result = fn()
  const end = performance.now()

  if (label) {
    console.log(`[Performance] ${label}: ${(end - start).toFixed(2)}ms`)
  }

  return result
}

/**
 * Measure execution time of an async function
 */
export async function measureTimeAsync<T>(fn: () => Promise<T>, label?: string): Promise<T> {
  const start = performance.now()
  const result = await fn()
  const end = performance.now()

  if (label) {
    console.log(`[Performance] ${label}: ${(end - start).toFixed(2)}ms`)
  }

  return result
}

/**
 * Debounce a function for performance
 */
export function debounce<T extends (...args: unknown[]) => unknown>(
  fn: T,
  delay: number
): (...args: Parameters<T>) => void {
  let timeoutId: ReturnType<typeof setTimeout> | null = null

  return function (this: unknown, ...args: Parameters<T>) {
    if (timeoutId) {
      clearTimeout(timeoutId)
    }

    timeoutId = setTimeout(() => {
      fn.apply(this, args)
      timeoutId = null
    }, delay)
  }
}

/**
 * Throttle a function for performance
 */
export function throttle<T extends (...args: unknown[]) => unknown>(
  fn: T,
  limit: number
): (...args: Parameters<T>) => void {
  let inThrottle = false

  return function (this: unknown, ...args: Parameters<T>) {
    if (!inThrottle) {
      fn.apply(this, args)
      inThrottle = true
      setTimeout(() => {
        inThrottle = false
      }, limit)
    }
  }
}

export default usePerformanceMonitor
