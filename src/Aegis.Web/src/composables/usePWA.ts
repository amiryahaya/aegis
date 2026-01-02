import { ref, computed, onMounted, onUnmounted } from 'vue'

// PWA composable for managing service worker updates and offline state
export function usePWA() {
  const needRefresh = ref(false)
  const offlineReady = ref(false)
  const isOnline = ref(navigator.onLine)
  const registration = ref<ServiceWorkerRegistration | null>(null)

  // Check if PWA is installed
  const isInstalled = computed(() => {
    return window.matchMedia('(display-mode: standalone)').matches ||
           (window.navigator as { standalone?: boolean }).standalone === true
  })

  // Check if app can be installed
  const canInstall = ref(false)
  let deferredPrompt: BeforeInstallPromptEvent | null = null

  interface BeforeInstallPromptEvent extends Event {
    prompt(): Promise<void>
    userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
  }

  // Handle online/offline events
  const handleOnline = () => {
    isOnline.value = true
  }

  const handleOffline = () => {
    isOnline.value = false
  }

  // Handle beforeinstallprompt event
  const handleBeforeInstallPrompt = (e: Event) => {
    e.preventDefault()
    deferredPrompt = e as BeforeInstallPromptEvent
    canInstall.value = true
  }

  // Handle appinstalled event
  const handleAppInstalled = () => {
    canInstall.value = false
    deferredPrompt = null
  }

  // Trigger PWA installation
  const install = async () => {
    if (!deferredPrompt) return false

    deferredPrompt.prompt()
    const { outcome } = await deferredPrompt.userChoice

    if (outcome === 'accepted') {
      canInstall.value = false
    }

    deferredPrompt = null
    return outcome === 'accepted'
  }

  // Update service worker
  const updateServiceWorker = async () => {
    if (registration.value && registration.value.waiting) {
      // Send skip waiting message to service worker
      registration.value.waiting.postMessage({ type: 'SKIP_WAITING' })
    }
    needRefresh.value = false
  }

  // Dismiss update prompt
  const dismissUpdate = () => {
    needRefresh.value = false
  }

  // Register service worker and handle updates
  const registerSW = async () => {
    if ('serviceWorker' in navigator) {
      try {
        const reg = await navigator.serviceWorker.register('/sw.js', {
          scope: '/'
        })

        registration.value = reg

        // Check for updates periodically
        setInterval(() => {
          reg.update()
        }, 60 * 60 * 1000) // Check every hour

        // Handle updates
        reg.addEventListener('updatefound', () => {
          const newWorker = reg.installing
          if (newWorker) {
            newWorker.addEventListener('statechange', () => {
              if (newWorker.state === 'installed' && navigator.serviceWorker.controller) {
                needRefresh.value = true
              }
            })
          }
        })

        // Handle controller change (when skipWaiting is called)
        let refreshing = false
        navigator.serviceWorker.addEventListener('controllerchange', () => {
          if (refreshing) return
          refreshing = true
          window.location.reload()
        })

        // Check if already offline ready
        if (reg.active && !navigator.serviceWorker.controller) {
          offlineReady.value = true
        }
      } catch (error) {
        console.error('Service worker registration failed:', error)
      }
    }
  }

  onMounted(() => {
    // Listen for online/offline events
    window.addEventListener('online', handleOnline)
    window.addEventListener('offline', handleOffline)

    // Listen for install prompt
    window.addEventListener('beforeinstallprompt', handleBeforeInstallPrompt)
    window.addEventListener('appinstalled', handleAppInstalled)

    // Register service worker (handled by vite-plugin-pwa in production)
    if (import.meta.env.PROD) {
      registerSW()
    }
  })

  onUnmounted(() => {
    window.removeEventListener('online', handleOnline)
    window.removeEventListener('offline', handleOffline)
    window.removeEventListener('beforeinstallprompt', handleBeforeInstallPrompt)
    window.removeEventListener('appinstalled', handleAppInstalled)
  })

  return {
    // State
    needRefresh,
    offlineReady,
    isOnline,
    isInstalled,
    canInstall,

    // Actions
    updateServiceWorker,
    dismissUpdate,
    install
  }
}
