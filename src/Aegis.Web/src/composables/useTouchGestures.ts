import { ref, onMounted, onUnmounted, type Ref } from 'vue'

export type SwipeDirection = 'left' | 'right' | 'up' | 'down' | null

export interface SwipeState {
  isSwiping: boolean
  direction: SwipeDirection
  deltaX: number
  deltaY: number
  startX: number
  startY: number
}

export interface SwipeOptions {
  threshold?: number // Minimum distance to trigger swipe
  preventScroll?: boolean // Prevent scroll during horizontal swipe
  onSwipeLeft?: () => void
  onSwipeRight?: () => void
  onSwipeUp?: () => void
  onSwipeDown?: () => void
  onSwipeEnd?: (direction: SwipeDirection) => void
}

/**
 * Composable for detecting swipe gestures
 */
export function useSwipe(
  target: Ref<HTMLElement | null>,
  options: SwipeOptions = {}
) {
  const {
    threshold = 50,
    preventScroll = false,
    onSwipeLeft,
    onSwipeRight,
    onSwipeUp,
    onSwipeDown,
    onSwipeEnd
  } = options

  const state = ref<SwipeState>({
    isSwiping: false,
    direction: null,
    deltaX: 0,
    deltaY: 0,
    startX: 0,
    startY: 0
  })

  function handleTouchStart(e: TouchEvent) {
    const touch = e.touches[0]
    state.value = {
      isSwiping: true,
      direction: null,
      deltaX: 0,
      deltaY: 0,
      startX: touch.clientX,
      startY: touch.clientY
    }
  }

  function handleTouchMove(e: TouchEvent) {
    if (!state.value.isSwiping) return

    const touch = e.touches[0]
    state.value.deltaX = touch.clientX - state.value.startX
    state.value.deltaY = touch.clientY - state.value.startY

    // Determine direction
    const absX = Math.abs(state.value.deltaX)
    const absY = Math.abs(state.value.deltaY)

    if (absX > absY) {
      state.value.direction = state.value.deltaX > 0 ? 'right' : 'left'
      if (preventScroll) {
        e.preventDefault()
      }
    } else {
      state.value.direction = state.value.deltaY > 0 ? 'down' : 'up'
    }
  }

  function handleTouchEnd() {
    if (!state.value.isSwiping) return

    const { deltaX, deltaY, direction } = state.value
    const absX = Math.abs(deltaX)
    const absY = Math.abs(deltaY)

    // Check if swipe threshold met
    if (absX >= threshold || absY >= threshold) {
      if (absX > absY) {
        if (deltaX > 0) {
          onSwipeRight?.()
        } else {
          onSwipeLeft?.()
        }
      } else {
        if (deltaY > 0) {
          onSwipeDown?.()
        } else {
          onSwipeUp?.()
        }
      }
      onSwipeEnd?.(direction)
    } else {
      onSwipeEnd?.(null)
    }

    // Reset state
    state.value.isSwiping = false
    state.value.direction = null
  }

  onMounted(() => {
    const el = target.value
    if (!el) return

    el.addEventListener('touchstart', handleTouchStart, { passive: true })
    el.addEventListener('touchmove', handleTouchMove, { passive: !preventScroll })
    el.addEventListener('touchend', handleTouchEnd, { passive: true })
  })

  onUnmounted(() => {
    const el = target.value
    if (!el) return

    el.removeEventListener('touchstart', handleTouchStart)
    el.removeEventListener('touchmove', handleTouchMove)
    el.removeEventListener('touchend', handleTouchEnd)
  })

  return {
    isSwiping: ref(state.value.isSwiping),
    direction: ref(state.value.direction),
    deltaX: ref(state.value.deltaX),
    deltaY: ref(state.value.deltaY)
  }
}

export interface PullToRefreshOptions {
  threshold?: number // Distance to trigger refresh
  maxPull?: number // Maximum pull distance
  onRefresh: () => Promise<void>
}

export interface PullToRefreshState {
  isPulling: boolean
  isRefreshing: boolean
  pullDistance: number
  progress: number // 0-1 progress to threshold
}

/**
 * Composable for pull-to-refresh gesture
 */
export function usePullToRefresh(
  target: Ref<HTMLElement | null>,
  options: PullToRefreshOptions
) {
  const { threshold = 80, maxPull = 120, onRefresh } = options

  const state = ref<PullToRefreshState>({
    isPulling: false,
    isRefreshing: false,
    pullDistance: 0,
    progress: 0
  })

  let startY = 0
  let isAtTop = false

  function checkIfAtTop(): boolean {
    const el = target.value
    if (!el) return false
    return el.scrollTop <= 0
  }

  function handleTouchStart(e: TouchEvent) {
    if (state.value.isRefreshing) return

    isAtTop = checkIfAtTop()
    if (!isAtTop) return

    startY = e.touches[0].clientY
    state.value.isPulling = true
  }

  function handleTouchMove(e: TouchEvent) {
    if (!state.value.isPulling || state.value.isRefreshing) return
    if (!isAtTop) return

    const currentY = e.touches[0].clientY
    const diff = currentY - startY

    if (diff > 0) {
      e.preventDefault()
      // Apply resistance (slower pull as distance increases)
      const resistance = Math.min(diff, maxPull)
      const dampedDistance = resistance * 0.5

      state.value.pullDistance = dampedDistance
      state.value.progress = Math.min(dampedDistance / threshold, 1)
    }
  }

  async function handleTouchEnd() {
    if (!state.value.isPulling) return

    if (state.value.pullDistance >= threshold && !state.value.isRefreshing) {
      state.value.isRefreshing = true
      state.value.pullDistance = threshold * 0.6 // Snap to smaller height

      try {
        await onRefresh()
      } finally {
        state.value.isRefreshing = false
        state.value.pullDistance = 0
        state.value.progress = 0
      }
    } else {
      state.value.pullDistance = 0
      state.value.progress = 0
    }

    state.value.isPulling = false
  }

  onMounted(() => {
    const el = target.value
    if (!el) return

    el.addEventListener('touchstart', handleTouchStart, { passive: true })
    el.addEventListener('touchmove', handleTouchMove, { passive: false })
    el.addEventListener('touchend', handleTouchEnd, { passive: true })
  })

  onUnmounted(() => {
    const el = target.value
    if (!el) return

    el.removeEventListener('touchstart', handleTouchStart)
    el.removeEventListener('touchmove', handleTouchMove)
    el.removeEventListener('touchend', handleTouchEnd)
  })

  return state
}

export interface LongPressOptions {
  delay?: number
  onLongPress: (event: TouchEvent) => void
  onCancel?: () => void
}

/**
 * Composable for long press gesture
 */
export function useLongPress(
  target: Ref<HTMLElement | null>,
  options: LongPressOptions
) {
  const { delay = 500, onLongPress, onCancel } = options

  const isLongPressing = ref(false)
  let timeoutId: ReturnType<typeof setTimeout> | null = null

  function handleTouchStart(e: TouchEvent) {
    timeoutId = setTimeout(() => {
      isLongPressing.value = true
      onLongPress(e)
    }, delay)
  }

  function handleTouchMove() {
    // Cancel if finger moves
    if (timeoutId) {
      clearTimeout(timeoutId)
      timeoutId = null
      if (isLongPressing.value) {
        onCancel?.()
      }
    }
    isLongPressing.value = false
  }

  function handleTouchEnd() {
    if (timeoutId) {
      clearTimeout(timeoutId)
      timeoutId = null
    }
    if (isLongPressing.value) {
      onCancel?.()
    }
    isLongPressing.value = false
  }

  onMounted(() => {
    const el = target.value
    if (!el) return

    el.addEventListener('touchstart', handleTouchStart, { passive: true })
    el.addEventListener('touchmove', handleTouchMove, { passive: true })
    el.addEventListener('touchend', handleTouchEnd, { passive: true })
    el.addEventListener('touchcancel', handleTouchEnd, { passive: true })
  })

  onUnmounted(() => {
    const el = target.value
    if (!el) return

    if (timeoutId) {
      clearTimeout(timeoutId)
    }

    el.removeEventListener('touchstart', handleTouchStart)
    el.removeEventListener('touchmove', handleTouchMove)
    el.removeEventListener('touchend', handleTouchEnd)
    el.removeEventListener('touchcancel', handleTouchEnd)
  })

  return {
    isLongPressing
  }
}

export interface PinchState {
  isPinching: boolean
  scale: number
  initialDistance: number
  currentDistance: number
}

/**
 * Composable for pinch-to-zoom gesture
 */
export function usePinch(
  target: Ref<HTMLElement | null>,
  options: {
    onPinch?: (scale: number) => void
    onPinchEnd?: (scale: number) => void
    minScale?: number
    maxScale?: number
  } = {}
) {
  const { onPinch, onPinchEnd, minScale = 0.5, maxScale = 3 } = options

  const state = ref<PinchState>({
    isPinching: false,
    scale: 1,
    initialDistance: 0,
    currentDistance: 0
  })

  function getDistance(touches: TouchList): number {
    const [t1, t2] = [touches[0], touches[1]]
    return Math.hypot(t2.clientX - t1.clientX, t2.clientY - t1.clientY)
  }

  function handleTouchStart(e: TouchEvent) {
    if (e.touches.length === 2) {
      state.value.isPinching = true
      state.value.initialDistance = getDistance(e.touches)
      state.value.currentDistance = state.value.initialDistance
    }
  }

  function handleTouchMove(e: TouchEvent) {
    if (!state.value.isPinching || e.touches.length !== 2) return

    e.preventDefault()
    state.value.currentDistance = getDistance(e.touches)

    const rawScale = state.value.currentDistance / state.value.initialDistance
    state.value.scale = Math.min(Math.max(rawScale, minScale), maxScale)

    onPinch?.(state.value.scale)
  }

  function handleTouchEnd(e: TouchEvent) {
    if (state.value.isPinching && e.touches.length < 2) {
      onPinchEnd?.(state.value.scale)
      state.value.isPinching = false
    }
  }

  onMounted(() => {
    const el = target.value
    if (!el) return

    el.addEventListener('touchstart', handleTouchStart, { passive: true })
    el.addEventListener('touchmove', handleTouchMove, { passive: false })
    el.addEventListener('touchend', handleTouchEnd, { passive: true })
  })

  onUnmounted(() => {
    const el = target.value
    if (!el) return

    el.removeEventListener('touchstart', handleTouchStart)
    el.removeEventListener('touchmove', handleTouchMove)
    el.removeEventListener('touchend', handleTouchEnd)
  })

  return state
}

export default useSwipe
