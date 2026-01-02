<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import AppLayout from '@/components/common/AppLayout.vue'
import PWAUpdatePrompt from '@/components/common/PWAUpdatePrompt.vue'
import ToastContainer from '@/components/common/ToastContainer.vue'
import SkipToContent from '@/components/common/SkipToContent.vue'
import ErrorBoundary from '@/components/common/ErrorBoundary.vue'
import { useErrorTracking } from '@/composables/useErrorTracking'

const route = useRoute()
const isAuthPage = computed(() => route.meta.requiresAuth === false)

// Initialize error tracking
const { trackCritical } = useErrorTracking()

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

    <!-- PWA update prompts and offline indicator -->
    <PWAUpdatePrompt />

    <!-- Toast notifications -->
    <ToastContainer />
  </div>
</template>
