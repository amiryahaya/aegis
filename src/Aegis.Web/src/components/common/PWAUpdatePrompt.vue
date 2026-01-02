<script setup lang="ts">
import { usePWA } from '@/composables/usePWA'
import { XMarkIcon, ArrowPathIcon, CloudArrowDownIcon } from '@heroicons/vue/24/outline'

const { needRefresh, offlineReady, isOnline, canInstall, updateServiceWorker, dismissUpdate, install } = usePWA()
</script>

<template>
  <!-- Offline indicator -->
  <Transition
    enter-active-class="transition-all duration-300"
    enter-from-class="opacity-0 -translate-y-2"
    enter-to-class="opacity-100 translate-y-0"
    leave-active-class="transition-all duration-300"
    leave-from-class="opacity-100 translate-y-0"
    leave-to-class="opacity-0 -translate-y-2"
  >
    <div
      v-if="!isOnline"
      class="fixed top-0 left-0 right-0 z-50 bg-yellow-500 text-yellow-900 px-4 py-2 text-center text-sm font-medium"
    >
      You're offline. Some features may be unavailable.
    </div>
  </Transition>

  <!-- Update available prompt -->
  <Transition
    enter-active-class="transition-all duration-300"
    enter-from-class="opacity-0 translate-y-4"
    enter-to-class="opacity-100 translate-y-0"
    leave-active-class="transition-all duration-300"
    leave-from-class="opacity-100 translate-y-0"
    leave-to-class="opacity-0 translate-y-4"
  >
    <div
      v-if="needRefresh"
      class="fixed bottom-4 right-4 z-50 max-w-sm bg-white dark:bg-gray-800 rounded-lg shadow-lg border border-gray-200 dark:border-gray-700 p-4"
    >
      <div class="flex items-start gap-3">
        <div class="flex-shrink-0">
          <ArrowPathIcon class="h-6 w-6 text-blue-500" />
        </div>
        <div class="flex-1 min-w-0">
          <p class="text-sm font-medium text-gray-900 dark:text-white">
            Update available
          </p>
          <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">
            A new version of AEGIS is available. Refresh to update.
          </p>
          <div class="mt-3 flex gap-2">
            <button
              @click="updateServiceWorker"
              class="px-3 py-1.5 text-sm font-medium text-white bg-blue-600 rounded-md hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              Refresh
            </button>
            <button
              @click="dismissUpdate"
              class="px-3 py-1.5 text-sm font-medium text-gray-700 dark:text-gray-300 bg-gray-100 dark:bg-gray-700 rounded-md hover:bg-gray-200 dark:hover:bg-gray-600 focus:outline-none focus:ring-2 focus:ring-gray-500"
            >
              Later
            </button>
          </div>
        </div>
        <button
          @click="dismissUpdate"
          class="flex-shrink-0 text-gray-400 hover:text-gray-500"
        >
          <XMarkIcon class="h-5 w-5" />
        </button>
      </div>
    </div>
  </Transition>

  <!-- Offline ready notification -->
  <Transition
    enter-active-class="transition-all duration-300"
    enter-from-class="opacity-0 translate-y-4"
    enter-to-class="opacity-100 translate-y-0"
    leave-active-class="transition-all duration-300"
    leave-from-class="opacity-100 translate-y-0"
    leave-to-class="opacity-0 translate-y-4"
  >
    <div
      v-if="offlineReady"
      class="fixed bottom-4 right-4 z-50 max-w-sm bg-white dark:bg-gray-800 rounded-lg shadow-lg border border-gray-200 dark:border-gray-700 p-4"
    >
      <div class="flex items-center gap-3">
        <div class="flex-shrink-0">
          <CloudArrowDownIcon class="h-6 w-6 text-green-500" />
        </div>
        <p class="text-sm text-gray-700 dark:text-gray-300">
          App ready to work offline
        </p>
      </div>
    </div>
  </Transition>

  <!-- Install prompt -->
  <Transition
    enter-active-class="transition-all duration-300"
    enter-from-class="opacity-0 translate-y-4"
    enter-to-class="opacity-100 translate-y-0"
    leave-active-class="transition-all duration-300"
    leave-from-class="opacity-100 translate-y-0"
    leave-to-class="opacity-0 translate-y-4"
  >
    <div
      v-if="canInstall"
      class="fixed bottom-4 left-4 z-50 max-w-sm bg-white dark:bg-gray-800 rounded-lg shadow-lg border border-gray-200 dark:border-gray-700 p-4"
    >
      <div class="flex items-start gap-3">
        <div class="flex-shrink-0">
          <CloudArrowDownIcon class="h-6 w-6 text-blue-500" />
        </div>
        <div class="flex-1 min-w-0">
          <p class="text-sm font-medium text-gray-900 dark:text-white">
            Install AEGIS
          </p>
          <p class="text-sm text-gray-500 dark:text-gray-400 mt-1">
            Install the app for a better experience
          </p>
          <div class="mt-3">
            <button
              @click="install"
              class="px-3 py-1.5 text-sm font-medium text-white bg-blue-600 rounded-md hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              Install
            </button>
          </div>
        </div>
      </div>
    </div>
  </Transition>
</template>
