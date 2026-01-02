import { onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'

interface ShortcutHandler {
  key: string
  ctrl?: boolean
  alt?: boolean
  shift?: boolean
  meta?: boolean
  handler: () => void
  description?: string
}

export function useKeyboardShortcuts() {
  const router = useRouter()
  const isEnabled = ref(true)
  const pendingKeys = ref<string[]>([])
  const pendingTimer = ref<number | null>(null)

  const shortcuts: ShortcutHandler[] = [
    // Navigation shortcuts (g + key pattern)
    { key: 'g h', handler: () => router.push('/'), description: 'Go to Dashboard' },
    { key: 'g c', handler: () => router.push('/chat'), description: 'Go to Chat' },
    { key: 'g s', handler: () => router.push('/sessions'), description: 'Go to Sessions' },
    { key: 'g w', handler: () => router.push('/workspaces'), description: 'Go to Workspaces' },
    { key: 'g n', handler: () => router.push('/notifications'), description: 'Go to Notifications' },
    { key: 'g p', handler: () => router.push('/profile'), description: 'Go to Profile' },
    { key: 'g ?', handler: () => router.push('/help'), description: 'Go to Help' },

    // Search (/ key focuses search input, Ctrl+K is handled by command palette)
    {
      key: '/',
      handler: () => {
        const searchInput = document.querySelector('input[placeholder*="Search"]') as HTMLInputElement
        if (searchInput) {
          searchInput.focus()
        } else {
          router.push('/search')
        }
      },
      description: 'Focus search'
    },

    // Theme toggle
    {
      key: 't',
      handler: () => {
        document.documentElement.classList.toggle('dark')
        const isDark = document.documentElement.classList.contains('dark')
        localStorage.setItem('theme', isDark ? 'dark' : 'light')
      },
      description: 'Toggle dark mode'
    },

    // Help
    {
      key: '?',
      shift: true,
      handler: () => router.push('/help'),
      description: 'Show help'
    },

    // Escape - close modals, clear search
    {
      key: 'Escape',
      handler: () => {
        const activeElement = document.activeElement as HTMLElement
        if (activeElement && activeElement.tagName === 'INPUT') {
          activeElement.blur()
        }
      },
      description: 'Clear / Close'
    }
  ]

  function handleKeyDown(event: KeyboardEvent) {
    if (!isEnabled.value) return

    // Ignore if user is typing in an input, textarea, or contenteditable
    const target = event.target as HTMLElement
    if (
      target.tagName === 'INPUT' ||
      target.tagName === 'TEXTAREA' ||
      target.isContentEditable
    ) {
      // Allow Escape key
      if (event.key !== 'Escape') return
    }

    // Check for single key shortcuts first
    for (const shortcut of shortcuts) {
      const keys = shortcut.key.split(' ')

      // Multi-key shortcut
      if (keys.length > 1) {
        continue // Handle below
      }

      // Single key shortcut
      const matchesKey = event.key.toLowerCase() === shortcut.key.toLowerCase() ||
        (shortcut.key === '/' && event.key === '/') ||
        (shortcut.key === '?' && event.key === '?' && event.shiftKey)

      const matchesModifiers =
        (shortcut.ctrl ?? false) === event.ctrlKey &&
        (shortcut.alt ?? false) === event.altKey &&
        (shortcut.shift ?? false) === event.shiftKey &&
        (shortcut.meta ?? false) === event.metaKey

      if (matchesKey && matchesModifiers) {
        event.preventDefault()
        shortcut.handler()
        return
      }
    }

    // Handle multi-key shortcuts (like "g h")
    const key = event.key.toLowerCase()

    // Clear pending timer
    if (pendingTimer.value !== null) {
      clearTimeout(pendingTimer.value)
    }

    // Add key to pending
    pendingKeys.value.push(key)

    // Check for matching multi-key shortcut
    const pendingString = pendingKeys.value.join(' ')
    for (const shortcut of shortcuts) {
      if (shortcut.key === pendingString) {
        event.preventDefault()
        shortcut.handler()
        pendingKeys.value = []
        return
      }

      // Check if any shortcut starts with pending keys
      if (shortcut.key.startsWith(pendingString + ' ')) {
        // Set timeout to clear pending keys
        pendingTimer.value = window.setTimeout(() => {
          pendingKeys.value = []
        }, 1000)
        return
      }
    }

    // No match and no potential match, clear pending
    pendingKeys.value = []
  }

  function enable() {
    isEnabled.value = true
  }

  function disable() {
    isEnabled.value = false
  }

  onMounted(() => {
    window.addEventListener('keydown', handleKeyDown)
  })

  onUnmounted(() => {
    window.removeEventListener('keydown', handleKeyDown)
    if (pendingTimer.value !== null) {
      clearTimeout(pendingTimer.value)
    }
  })

  return {
    isEnabled,
    enable,
    disable,
    shortcuts
  }
}
