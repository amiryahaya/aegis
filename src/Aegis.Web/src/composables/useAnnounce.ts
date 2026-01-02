import { ref, onMounted } from 'vue'

export type AnnounceMode = 'polite' | 'assertive'

// Live region element reference
let liveRegionPolite: HTMLElement | null = null
let liveRegionAssertive: HTMLElement | null = null

// Create live region elements
const createLiveRegions = () => {
  if (typeof document === 'undefined') return

  // Polite announcements (wait for idle)
  if (!liveRegionPolite) {
    liveRegionPolite = document.createElement('div')
    liveRegionPolite.setAttribute('aria-live', 'polite')
    liveRegionPolite.setAttribute('aria-atomic', 'true')
    liveRegionPolite.setAttribute('role', 'status')
    liveRegionPolite.className = 'sr-only'
    liveRegionPolite.id = 'aria-live-polite'
    document.body.appendChild(liveRegionPolite)
  }

  // Assertive announcements (interrupt)
  if (!liveRegionAssertive) {
    liveRegionAssertive = document.createElement('div')
    liveRegionAssertive.setAttribute('aria-live', 'assertive')
    liveRegionAssertive.setAttribute('aria-atomic', 'true')
    liveRegionAssertive.setAttribute('role', 'alert')
    liveRegionAssertive.className = 'sr-only'
    liveRegionAssertive.id = 'aria-live-assertive'
    document.body.appendChild(liveRegionAssertive)
  }
}

// Remove live regions (exported for cleanup if needed)
export const removeLiveRegions = () => {
  if (liveRegionPolite) {
    liveRegionPolite.remove()
    liveRegionPolite = null
  }
  if (liveRegionAssertive) {
    liveRegionAssertive.remove()
    liveRegionAssertive = null
  }
}

// Announce message to screen readers
export const announce = (message: string, mode: AnnounceMode = 'polite') => {
  createLiveRegions()

  const region = mode === 'assertive' ? liveRegionAssertive : liveRegionPolite
  if (!region) return

  // Clear and set new message
  // Using a timeout ensures screen readers pick up the change
  region.textContent = ''
  requestAnimationFrame(() => {
    region.textContent = message
  })
}

// Clear announcements
export const clearAnnouncements = () => {
  if (liveRegionPolite) liveRegionPolite.textContent = ''
  if (liveRegionAssertive) liveRegionAssertive.textContent = ''
}

// Composable
export function useAnnounce() {
  const lastMessage = ref('')
  const lastMode = ref<AnnounceMode>('polite')

  // Ensure live regions exist
  onMounted(() => {
    createLiveRegions()
  })

  // Announce with tracking
  const announceMessage = (message: string, mode: AnnounceMode = 'polite') => {
    lastMessage.value = message
    lastMode.value = mode
    announce(message, mode)
  }

  // Convenience methods
  const polite = (message: string) => announceMessage(message, 'polite')
  const assertive = (message: string) => announceMessage(message, 'assertive')

  // Announce loading state
  const announceLoading = (resource: string) => {
    polite(`Loading ${resource}...`)
  }

  // Announce completion
  const announceComplete = (action: string) => {
    polite(`${action} complete`)
  }

  // Announce error
  const announceError = (message: string) => {
    assertive(`Error: ${message}`)
  }

  // Announce navigation
  const announceNavigation = (page: string) => {
    polite(`Navigated to ${page}`)
  }

  // Clear current announcement
  const clear = () => {
    lastMessage.value = ''
    clearAnnouncements()
  }

  return {
    lastMessage,
    lastMode,
    announce: announceMessage,
    polite,
    assertive,
    announceLoading,
    announceComplete,
    announceError,
    announceNavigation,
    clear
  }
}

export default useAnnounce
