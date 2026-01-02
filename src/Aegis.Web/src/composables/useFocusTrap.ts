import { ref, watch, onUnmounted, type Ref } from 'vue'

// Focusable element selectors
const FOCUSABLE_SELECTORS = [
  'a[href]',
  'area[href]',
  'input:not([disabled]):not([type="hidden"])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  'button:not([disabled])',
  'iframe',
  'object',
  'embed',
  '[contenteditable]',
  '[tabindex]:not([tabindex="-1"])'
].join(',')

export interface FocusTrapOptions {
  initialFocus?: string | HTMLElement | null
  returnFocus?: boolean
  escapeDeactivates?: boolean
  onEscape?: () => void
}

export function useFocusTrap(
  containerRef: Ref<HTMLElement | null>,
  options: FocusTrapOptions = {}
) {
  const {
    initialFocus = null,
    returnFocus = true,
    escapeDeactivates = true,
    onEscape
  } = options

  const isActive = ref(false)
  let previousActiveElement: HTMLElement | null = null

  // Get all focusable elements within container
  const getFocusableElements = (): HTMLElement[] => {
    if (!containerRef.value) return []
    return Array.from(
      containerRef.value.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTORS)
    ).filter(el => {
      // Filter out hidden elements
      return el.offsetParent !== null && !el.hasAttribute('disabled')
    })
  }

  // Focus the first focusable element or specified initial element
  const focusInitial = () => {
    if (!containerRef.value) return

    let elementToFocus: HTMLElement | null = null

    if (typeof initialFocus === 'string') {
      elementToFocus = containerRef.value.querySelector(initialFocus)
    } else if (initialFocus instanceof HTMLElement) {
      elementToFocus = initialFocus
    }

    if (!elementToFocus) {
      const focusableElements = getFocusableElements()
      elementToFocus = focusableElements[0] || containerRef.value
    }

    elementToFocus?.focus()
  }

  // Handle tab key to trap focus
  const handleKeyDown = (event: KeyboardEvent) => {
    if (!isActive.value || !containerRef.value) return

    if (event.key === 'Escape' && escapeDeactivates) {
      event.preventDefault()
      onEscape?.()
      return
    }

    if (event.key !== 'Tab') return

    const focusableElements = getFocusableElements()
    if (focusableElements.length === 0) return

    const firstElement = focusableElements[0]
    const lastElement = focusableElements[focusableElements.length - 1]

    // Shift + Tab on first element -> focus last
    if (event.shiftKey && document.activeElement === firstElement) {
      event.preventDefault()
      lastElement.focus()
      return
    }

    // Tab on last element -> focus first
    if (!event.shiftKey && document.activeElement === lastElement) {
      event.preventDefault()
      firstElement.focus()
      return
    }

    // If focus is outside container, bring it back
    if (!containerRef.value.contains(document.activeElement)) {
      event.preventDefault()
      firstElement.focus()
    }
  }

  // Activate focus trap
  const activate = () => {
    if (isActive.value) return

    // Store current active element
    previousActiveElement = document.activeElement as HTMLElement

    isActive.value = true
    document.addEventListener('keydown', handleKeyDown)

    // Focus initial element
    requestAnimationFrame(() => {
      focusInitial()
    })
  }

  // Deactivate focus trap
  const deactivate = () => {
    if (!isActive.value) return

    isActive.value = false
    document.removeEventListener('keydown', handleKeyDown)

    // Return focus to previous element
    if (returnFocus && previousActiveElement) {
      previousActiveElement.focus()
      previousActiveElement = null
    }
  }

  // Pause trap (allow focus outside temporarily)
  const pause = () => {
    document.removeEventListener('keydown', handleKeyDown)
  }

  // Resume trap
  const resume = () => {
    if (isActive.value) {
      document.addEventListener('keydown', handleKeyDown)
    }
  }

  // Watch for container changes
  watch(containerRef, (newContainer) => {
    if (newContainer && isActive.value) {
      focusInitial()
    }
  })

  // Cleanup on unmount
  onUnmounted(() => {
    deactivate()
  })

  return {
    isActive,
    activate,
    deactivate,
    pause,
    resume,
    focusInitial
  }
}

export default useFocusTrap
