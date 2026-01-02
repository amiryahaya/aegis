<script setup lang="ts">
import { computed } from 'vue'
import { Popover, PopoverButton, PopoverPanel } from '@headlessui/vue'
import {
  WifiIcon,
  ExclamationTriangleIcon,
  ArrowPathIcon
} from '@heroicons/vue/24/outline'
import { useConnection } from '@/composables/useConnection'
import {
  CONNECTION_STATE_LABELS,
  CONNECTION_STATE_COLORS
} from '@/types/connection'

interface Props {
  showLabel?: boolean
  size?: 'sm' | 'md' | 'lg'
}

const props = withDefaults(defineProps<Props>(), {
  showLabel: false,
  size: 'md'
})

const {
  connectionStatus,
  state,
  isConnected,
  isConnecting,
  hasError,
  reconnectAttempts,
  retry
} = useConnection()

const colors = computed(() => CONNECTION_STATE_COLORS[state.value])
const label = computed(() => CONNECTION_STATE_LABELS[state.value])

const iconSize = computed(() => {
  switch (props.size) {
    case 'sm': return 'h-3.5 w-3.5'
    case 'lg': return 'h-5 w-5'
    default: return 'h-4 w-4'
  }
})

const dotSize = computed(() => {
  switch (props.size) {
    case 'sm': return 'h-1.5 w-1.5'
    case 'lg': return 'h-2.5 w-2.5'
    default: return 'h-2 w-2'
  }
})

const textSize = computed(() => {
  switch (props.size) {
    case 'sm': return 'text-xs'
    case 'lg': return 'text-sm'
    default: return 'text-xs'
  }
})

function formatDate(date?: Date): string {
  if (!date) return 'Never'
  return new Intl.DateTimeFormat('en-US', {
    hour: 'numeric',
    minute: '2-digit',
    second: '2-digit'
  }).format(date)
}
</script>

<template>
  <Popover class="relative">
    <PopoverButton
      class="flex items-center gap-1.5 rounded-lg px-2 py-1 transition-colors focus:outline-none focus:ring-2 focus:ring-aegis-500 focus:ring-offset-2"
      :class="[colors.bg, colors.text]"
    >
      <!-- Status dot with pulse animation for connecting states -->
      <span class="relative flex items-center justify-center">
        <span
          v-if="isConnecting"
          class="absolute animate-ping rounded-full opacity-75"
          :class="[dotSize, colors.dot]"
        />
        <span
          class="relative rounded-full"
          :class="[dotSize, colors.dot]"
        />
      </span>

      <!-- Icon -->
      <WifiIcon
        v-if="isConnected"
        :class="iconSize"
      />
      <ArrowPathIcon
        v-else-if="isConnecting"
        :class="[iconSize, 'animate-spin']"
      />
      <ExclamationTriangleIcon
        v-else-if="hasError"
        :class="iconSize"
      />
      <WifiIcon
        v-else
        :class="[iconSize, 'opacity-50']"
      />

      <!-- Label -->
      <span
        v-if="showLabel"
        :class="textSize"
      >
        {{ label }}
      </span>
    </PopoverButton>

    <transition
      enter-active-class="transition duration-200 ease-out"
      enter-from-class="translate-y-1 opacity-0"
      enter-to-class="translate-y-0 opacity-100"
      leave-active-class="transition duration-150 ease-in"
      leave-from-class="translate-y-0 opacity-100"
      leave-to-class="translate-y-1 opacity-0"
    >
      <PopoverPanel
        class="absolute right-0 z-10 mt-2 w-64 rounded-lg bg-white p-4 shadow-lg ring-1 ring-black ring-opacity-5 dark:bg-gray-800 dark:ring-gray-700"
      >
        <div class="space-y-3">
          <!-- Status header -->
          <div class="flex items-center justify-between">
            <h3 class="text-sm font-medium text-gray-900 dark:text-white">
              Connection Status
            </h3>
            <span
              class="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium"
              :class="[colors.bg, colors.text]"
            >
              <span
                class="h-1.5 w-1.5 rounded-full"
                :class="colors.dot"
              />
              {{ label }}
            </span>
          </div>

          <!-- Details -->
          <div class="space-y-2 text-sm">
            <div class="flex justify-between">
              <span class="text-gray-500 dark:text-gray-400">Last connected</span>
              <span class="text-gray-900 dark:text-white">
                {{ formatDate(connectionStatus.lastConnected) }}
              </span>
            </div>

            <div v-if="connectionStatus.lastDisconnected" class="flex justify-between">
              <span class="text-gray-500 dark:text-gray-400">Last disconnected</span>
              <span class="text-gray-900 dark:text-white">
                {{ formatDate(connectionStatus.lastDisconnected) }}
              </span>
            </div>

            <div v-if="isConnecting" class="flex justify-between">
              <span class="text-gray-500 dark:text-gray-400">Reconnect attempts</span>
              <span class="text-gray-900 dark:text-white">
                {{ reconnectAttempts }}
              </span>
            </div>

            <div v-if="connectionStatus.error" class="mt-2 rounded-md bg-red-50 p-2 dark:bg-red-900/20">
              <p class="text-xs text-red-700 dark:text-red-400">
                {{ connectionStatus.error }}
              </p>
            </div>
          </div>

          <!-- Retry button -->
          <button
            v-if="hasError || state === 'disconnected'"
            class="w-full rounded-lg bg-aegis-600 px-3 py-2 text-sm font-medium text-white hover:bg-aegis-700 focus:outline-none focus:ring-2 focus:ring-aegis-500 focus:ring-offset-2"
            @click="retry"
          >
            Retry Connection
          </button>
        </div>
      </PopoverPanel>
    </transition>
  </Popover>
</template>
