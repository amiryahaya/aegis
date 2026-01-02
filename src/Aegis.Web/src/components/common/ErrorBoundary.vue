<script setup lang="ts">
import { ref, onErrorCaptured, provide } from 'vue'
import { ExclamationTriangleIcon, ArrowPathIcon } from '@heroicons/vue/24/outline'

interface Props {
  fallbackMessage?: string
  showDetails?: boolean
  onError?: (error: Error, info: string) => void
}

const props = withDefaults(defineProps<Props>(), {
  fallbackMessage: 'Something went wrong',
  showDetails: false
})

const hasError = ref(false)
const error = ref<Error | null>(null)
const errorInfo = ref<string>('')

// Capture errors from child components
onErrorCaptured((err: Error, instance, info: string) => {
  hasError.value = true
  error.value = err
  errorInfo.value = info

  // Call optional error handler
  if (props.onError) {
    props.onError(err, info)
  }

  // Log error for debugging
  console.error('[ErrorBoundary] Caught error:', err)
  console.error('[ErrorBoundary] Component:', instance)
  console.error('[ErrorBoundary] Info:', info)

  // Return false to prevent error from propagating
  return false
})

// Reset error state
const reset = () => {
  hasError.value = false
  error.value = null
  errorInfo.value = ''
}

// Reload the page
const reload = () => {
  window.location.reload()
}

// Provide reset function to child components
provide('errorBoundaryReset', reset)
</script>

<template>
  <div v-if="hasError" class="min-h-[200px] flex items-center justify-center p-6">
    <div class="max-w-md w-full bg-white dark:bg-gray-800 rounded-lg shadow-lg border border-red-200 dark:border-red-800 p-6">
      <div class="flex items-center gap-3 mb-4">
        <div class="flex-shrink-0 w-12 h-12 bg-red-100 dark:bg-red-900/30 rounded-full flex items-center justify-center">
          <ExclamationTriangleIcon class="w-6 h-6 text-red-600 dark:text-red-400" />
        </div>
        <div>
          <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
            {{ fallbackMessage }}
          </h3>
          <p class="text-sm text-gray-500 dark:text-gray-400">
            An error occurred while rendering this component
          </p>
        </div>
      </div>

      <!-- Error details (optional) -->
      <div v-if="showDetails && error" class="mb-4">
        <details class="group">
          <summary class="cursor-pointer text-sm font-medium text-gray-700 dark:text-gray-300 hover:text-gray-900 dark:hover:text-white">
            Show error details
          </summary>
          <div class="mt-2 p-3 bg-gray-50 dark:bg-gray-900 rounded-md overflow-auto max-h-40">
            <p class="text-sm font-mono text-red-600 dark:text-red-400">
              {{ error.name }}: {{ error.message }}
            </p>
            <pre v-if="error.stack" class="mt-2 text-xs text-gray-600 dark:text-gray-400 whitespace-pre-wrap">{{ error.stack }}</pre>
          </div>
        </details>
      </div>

      <!-- Actions -->
      <div class="flex gap-3">
        <button
          @click="reset"
          class="flex-1 inline-flex items-center justify-center gap-2 px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-200 bg-gray-100 dark:bg-gray-700 rounded-md hover:bg-gray-200 dark:hover:bg-gray-600 focus:outline-none focus:ring-2 focus:ring-gray-500"
        >
          <ArrowPathIcon class="w-4 h-4" />
          Try Again
        </button>
        <button
          @click="reload"
          class="flex-1 inline-flex items-center justify-center gap-2 px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-md hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          Reload Page
        </button>
      </div>
    </div>
  </div>

  <!-- Render children when no error -->
  <slot v-else />
</template>
