<script setup lang="ts">
import { useToast, type Toast } from '@/composables/useToast'
import {
  CheckCircleIcon,
  XCircleIcon,
  ExclamationTriangleIcon,
  InformationCircleIcon,
  XMarkIcon
} from '@heroicons/vue/24/outline'

const { toasts, remove } = useToast()

const getIcon = (type: Toast['type']) => {
  switch (type) {
    case 'success':
      return CheckCircleIcon
    case 'error':
      return XCircleIcon
    case 'warning':
      return ExclamationTriangleIcon
    case 'info':
    default:
      return InformationCircleIcon
  }
}

const getStyles = (type: Toast['type']) => {
  switch (type) {
    case 'success':
      return {
        container: 'bg-green-50 dark:bg-green-900/30 border-green-200 dark:border-green-800',
        icon: 'text-green-500 dark:text-green-400',
        title: 'text-green-800 dark:text-green-200',
        message: 'text-green-700 dark:text-green-300'
      }
    case 'error':
      return {
        container: 'bg-red-50 dark:bg-red-900/30 border-red-200 dark:border-red-800',
        icon: 'text-red-500 dark:text-red-400',
        title: 'text-red-800 dark:text-red-200',
        message: 'text-red-700 dark:text-red-300'
      }
    case 'warning':
      return {
        container: 'bg-yellow-50 dark:bg-yellow-900/30 border-yellow-200 dark:border-yellow-800',
        icon: 'text-yellow-500 dark:text-yellow-400',
        title: 'text-yellow-800 dark:text-yellow-200',
        message: 'text-yellow-700 dark:text-yellow-300'
      }
    case 'info':
    default:
      return {
        container: 'bg-blue-50 dark:bg-blue-900/30 border-blue-200 dark:border-blue-800',
        icon: 'text-blue-500 dark:text-blue-400',
        title: 'text-blue-800 dark:text-blue-200',
        message: 'text-blue-700 dark:text-blue-300'
      }
  }
}
</script>

<template>
  <Teleport to="body">
    <div
      aria-live="polite"
      aria-label="Notifications"
      class="fixed bottom-4 right-4 z-50 flex flex-col gap-2 max-w-sm w-full pointer-events-none"
    >
      <TransitionGroup
        enter-active-class="transition-all duration-300 ease-out"
        enter-from-class="opacity-0 translate-x-4"
        enter-to-class="opacity-100 translate-x-0"
        leave-active-class="transition-all duration-200 ease-in"
        leave-from-class="opacity-100 translate-x-0"
        leave-to-class="opacity-0 translate-x-4"
        move-class="transition-all duration-300"
      >
        <div
          v-for="toast in toasts"
          :key="toast.id"
          role="alert"
          :class="[
            'pointer-events-auto rounded-lg border shadow-lg p-4',
            getStyles(toast.type).container
          ]"
        >
          <div class="flex items-start gap-3">
            <!-- Icon -->
            <component
              :is="getIcon(toast.type)"
              :class="['w-5 h-5 flex-shrink-0 mt-0.5', getStyles(toast.type).icon]"
            />

            <!-- Content -->
            <div class="flex-1 min-w-0">
              <p :class="['text-sm font-medium', getStyles(toast.type).title]">
                {{ toast.title }}
              </p>
              <p
                v-if="toast.message"
                :class="['mt-1 text-sm', getStyles(toast.type).message]"
              >
                {{ toast.message }}
              </p>

              <!-- Action button -->
              <button
                v-if="toast.action"
                @click="toast.action.onClick"
                :class="[
                  'mt-2 text-sm font-medium underline hover:no-underline focus:outline-none focus:ring-2 focus:ring-offset-2 rounded',
                  getStyles(toast.type).title
                ]"
              >
                {{ toast.action.label }}
              </button>
            </div>

            <!-- Dismiss button -->
            <button
              v-if="toast.dismissible"
              @click="remove(toast.id)"
              :class="[
                'flex-shrink-0 rounded-md p-1 hover:bg-black/5 dark:hover:bg-white/5 focus:outline-none focus:ring-2 focus:ring-offset-2',
                getStyles(toast.type).icon
              ]"
              aria-label="Dismiss notification"
            >
              <XMarkIcon class="w-4 h-4" />
            </button>
          </div>
        </div>
      </TransitionGroup>
    </div>
  </Teleport>
</template>
