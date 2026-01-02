<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import AppLayout from '@/components/common/AppLayout.vue'
import PWAUpdatePrompt from '@/components/common/PWAUpdatePrompt.vue'
import ToastContainer from '@/components/common/ToastContainer.vue'
import SkipToContent from '@/components/common/SkipToContent.vue'
import ErrorBoundary from '@/components/common/ErrorBoundary.vue'
import CommandPalette from '@/components/common/CommandPalette.vue'
import OfflineIndicator from '@/components/common/OfflineIndicator.vue'
import OnboardingModal from '@/components/onboarding/OnboardingModal.vue'
import FeatureTour from '@/components/onboarding/FeatureTour.vue'
import MobileBottomNav from '@/components/mobile/MobileBottomNav.vue'
import { useErrorTracking } from '@/composables/useErrorTracking'
import { useCommandPalette } from '@/composables/useCommandPalette'
import { useOnboarding } from '@/composables/useOnboarding'
import { useOfflineQueue } from '@/composables/useOfflineQueue'

const route = useRoute()
const isAuthPage = computed(() => route.meta.requiresAuth === false)

// Initialize error tracking
const { trackCritical } = useErrorTracking()

// Initialize command palette (registers keyboard shortcuts)
useCommandPalette()

// Initialize offline queue (syncs pending operations)
useOfflineQueue()

// Initialize onboarding
const { shouldShowOnboarding, showWelcome } = useOnboarding()

// Show onboarding for new users
onMounted(() => {
  if (shouldShowOnboarding() && !isAuthPage.value) {
    // Delay slightly to let the app render first
    setTimeout(() => {
      showWelcome()
    }, 500)
  }
})

// Handle critical errors from error boundary
const handleCriticalError = (error: Error, info: string) => {
  trackCritical(error, {
    action: 'ErrorBoundary',
    extra: { info }
  })
}
</script>

<template>
  <div class="min-h-screen">
    <!-- Skip to content link for keyboard navigation -->
    <SkipToContent />

    <!-- Global error boundary -->
    <ErrorBoundary
      fallback-message="Something went wrong"
      :show-details="true"
      :on-error="handleCriticalError"
    >
      <component :is="isAuthPage ? 'div' : AppLayout">
        <main id="main-content" tabindex="-1" class="outline-none">
          <RouterView />
        </main>
      </component>
    </ErrorBoundary>

    <!-- PWA update prompts -->
    <PWAUpdatePrompt />

    <!-- Offline status indicator -->
    <OfflineIndicator />

    <!-- Toast notifications -->
    <ToastContainer />

    <!-- Command Palette (Cmd+K / Ctrl+K) -->
    <CommandPalette />

    <!-- Onboarding -->
    <OnboardingModal />
    <FeatureTour />

    <!-- Mobile bottom navigation -->
    <MobileBottomNav v-if="!isAuthPage" />
  </div>
</template>

<style>
/* Add padding for mobile bottom navigation */
@media (max-width: 767px) {
  #main-content {
    padding-bottom: calc(4rem + env(safe-area-inset-bottom, 0px));
  }
}
</style>
