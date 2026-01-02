import { ref, onMounted, onUnmounted, computed } from 'vue'

// Standard breakpoints matching TailwindCSS
export const breakpoints = {
  sm: 640,
  md: 768,
  lg: 1024,
  xl: 1280,
  '2xl': 1536
} as const

export type Breakpoint = keyof typeof breakpoints

/**
 * Composable for responsive media queries
 * Matches TailwindCSS breakpoints
 */
export function useMediaQuery(query: string) {
  const matches = ref(false)
  let mediaQueryList: MediaQueryList | null = null

  function updateMatches(e: MediaQueryListEvent | MediaQueryList) {
    matches.value = e.matches
  }

  onMounted(() => {
    mediaQueryList = window.matchMedia(query)
    matches.value = mediaQueryList.matches

    // Modern browsers
    if (mediaQueryList.addEventListener) {
      mediaQueryList.addEventListener('change', updateMatches)
    } else {
      // Legacy browsers
      mediaQueryList.addListener(updateMatches)
    }
  })

  onUnmounted(() => {
    if (mediaQueryList) {
      if (mediaQueryList.removeEventListener) {
        mediaQueryList.removeEventListener('change', updateMatches)
      } else {
        mediaQueryList.removeListener(updateMatches)
      }
    }
  })

  return matches
}

/**
 * Composable for breakpoint-based responsive design
 * Returns reactive breakpoint states matching TailwindCSS
 */
export function useBreakpoints() {
  const width = ref(typeof window !== 'undefined' ? window.innerWidth : 0)

  function updateWidth() {
    width.value = window.innerWidth
  }

  onMounted(() => {
    updateWidth()
    window.addEventListener('resize', updateWidth)
  })

  onUnmounted(() => {
    window.removeEventListener('resize', updateWidth)
  })

  // Breakpoint checks (min-width based, like Tailwind)
  const isSm = computed(() => width.value >= breakpoints.sm)
  const isMd = computed(() => width.value >= breakpoints.md)
  const isLg = computed(() => width.value >= breakpoints.lg)
  const isXl = computed(() => width.value >= breakpoints.xl)
  const is2Xl = computed(() => width.value >= breakpoints['2xl'])

  // Device type helpers
  const isMobile = computed(() => width.value < breakpoints.md)
  const isTablet = computed(() => width.value >= breakpoints.md && width.value < breakpoints.lg)
  const isDesktop = computed(() => width.value >= breakpoints.lg)

  // Current breakpoint
  const current = computed<Breakpoint | 'xs'>(() => {
    if (width.value >= breakpoints['2xl']) return '2xl'
    if (width.value >= breakpoints.xl) return 'xl'
    if (width.value >= breakpoints.lg) return 'lg'
    if (width.value >= breakpoints.md) return 'md'
    if (width.value >= breakpoints.sm) return 'sm'
    return 'xs'
  })

  // Check if at or above a specific breakpoint
  function isAtLeast(breakpoint: Breakpoint): boolean {
    return width.value >= breakpoints[breakpoint]
  }

  // Check if below a specific breakpoint
  function isBelow(breakpoint: Breakpoint): boolean {
    return width.value < breakpoints[breakpoint]
  }

  return {
    width,
    // Breakpoint states
    isSm,
    isMd,
    isLg,
    isXl,
    is2Xl,
    // Device types
    isMobile,
    isTablet,
    isDesktop,
    // Current breakpoint
    current,
    // Utility functions
    isAtLeast,
    isBelow,
    // Constants
    breakpoints
  }
}

/**
 * Composable for detecting touch device
 */
export function useTouchDevice() {
  const isTouchDevice = ref(false)
  const hasCoarsePointer = ref(false)

  onMounted(() => {
    // Check for touch capability
    isTouchDevice.value = 'ontouchstart' in window || navigator.maxTouchPoints > 0

    // Check for coarse pointer (touch vs mouse)
    if (window.matchMedia) {
      hasCoarsePointer.value = window.matchMedia('(pointer: coarse)').matches
    }
  })

  return {
    isTouchDevice,
    hasCoarsePointer
  }
}

/**
 * Composable for detecting device orientation
 */
export function useOrientation() {
  const isPortrait = ref(true)
  const isLandscape = ref(false)
  const angle = ref(0)

  function updateOrientation() {
    if (window.screen.orientation) {
      angle.value = window.screen.orientation.angle
      isPortrait.value = window.screen.orientation.type.includes('portrait')
      isLandscape.value = window.screen.orientation.type.includes('landscape')
    } else {
      // Fallback for older browsers
      isPortrait.value = window.innerHeight > window.innerWidth
      isLandscape.value = window.innerWidth > window.innerHeight
      angle.value = isPortrait.value ? 0 : 90
    }
  }

  onMounted(() => {
    updateOrientation()

    if (window.screen.orientation) {
      window.screen.orientation.addEventListener('change', updateOrientation)
    } else {
      window.addEventListener('orientationchange', updateOrientation)
      window.addEventListener('resize', updateOrientation)
    }
  })

  onUnmounted(() => {
    if (window.screen.orientation) {
      window.screen.orientation.removeEventListener('change', updateOrientation)
    } else {
      window.removeEventListener('orientationchange', updateOrientation)
      window.removeEventListener('resize', updateOrientation)
    }
  })

  return {
    isPortrait,
    isLandscape,
    angle
  }
}

export default useBreakpoints
