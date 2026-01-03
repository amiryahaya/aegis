<script setup lang="ts">
import {
  Dialog,
  DialogPanel,
  DialogTitle,
  TransitionRoot,
  TransitionChild
} from '@headlessui/vue'
import {
  XMarkIcon,
  CheckCircleIcon,
  XCircleIcon,
  ClipboardDocumentIcon
} from '@heroicons/vue/24/outline'
import type { DetailedAuditLogEntry } from '@/types'
import {
  getActionLabel,
  getCategoryLabel,
  getSeverityColor,
  formatTimestamp
} from '@/types/audit'
import { useToast } from '@/composables/useToast'

defineProps<{
  isOpen: boolean
  entry: DetailedAuditLogEntry | null
}>()

const emit = defineEmits<{
  close: []
}>()

const toast = useToast()

function getSeverityClasses(color: string): string {
  switch (color) {
    case 'blue':
      return 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400'
    case 'yellow':
      return 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400'
    case 'red':
      return 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400'
    case 'purple':
      return 'bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-400'
    default:
      return 'bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-400'
  }
}

async function copyToClipboard(text: string) {
  try {
    await navigator.clipboard.writeText(text)
    toast.success('Copied to clipboard')
  } catch {
    toast.error('Failed to copy')
  }
}
</script>

<template>
  <TransitionRoot appear :show="isOpen" as="template">
    <Dialog as="div" class="relative z-50" @close="emit('close')">
      <TransitionChild
        as="template"
        enter="duration-300 ease-out"
        enter-from="opacity-0"
        enter-to="opacity-100"
        leave="duration-200 ease-in"
        leave-from="opacity-100"
        leave-to="opacity-0"
      >
        <div class="fixed inset-0 bg-black/25 dark:bg-black/50" />
      </TransitionChild>

      <div class="fixed inset-0 overflow-y-auto">
        <div class="flex min-h-full items-center justify-center p-4">
          <TransitionChild
            as="template"
            enter="duration-300 ease-out"
            enter-from="opacity-0 scale-95"
            enter-to="opacity-100 scale-100"
            leave="duration-200 ease-in"
            leave-from="opacity-100 scale-100"
            leave-to="opacity-0 scale-95"
          >
            <DialogPanel
              class="w-full max-w-2xl transform overflow-hidden rounded-xl bg-white shadow-xl transition-all dark:bg-gray-800"
            >
              <!-- Header -->
              <div class="flex items-center justify-between border-b px-6 py-4 dark:border-gray-700">
                <DialogTitle class="text-lg font-semibold text-gray-900 dark:text-white">
                  Audit Log Details
                </DialogTitle>
                <button
                  type="button"
                  class="rounded-lg p-1.5 text-gray-400 hover:bg-gray-100 hover:text-gray-500 dark:hover:bg-gray-700"
                  @click="emit('close')"
                >
                  <XMarkIcon class="h-5 w-5" />
                </button>
              </div>

              <!-- Content -->
              <div v-if="entry" class="max-h-[70vh] overflow-y-auto p-6">
                <!-- Status Banner -->
                <div
                  class="flex items-center gap-3 rounded-lg p-4"
                  :class="{
                    'bg-green-50 dark:bg-green-900/20': entry.success,
                    'bg-red-50 dark:bg-red-900/20': !entry.success
                  }"
                >
                  <CheckCircleIcon
                    v-if="entry.success"
                    class="h-8 w-8 text-green-500"
                  />
                  <XCircleIcon v-else class="h-8 w-8 text-red-500" />
                  <div>
                    <p
                      class="font-semibold"
                      :class="{
                        'text-green-800 dark:text-green-200': entry.success,
                        'text-red-800 dark:text-red-200': !entry.success
                      }"
                    >
                      {{ getActionLabel(entry.action) }}
                    </p>
                    <p class="text-sm text-gray-500 dark:text-gray-400">
                      {{ entry.description }}
                    </p>
                  </div>
                </div>

                <!-- Details Grid -->
                <div class="mt-6 grid grid-cols-2 gap-4">
                  <div>
                    <label class="text-xs font-medium uppercase text-gray-500 dark:text-gray-400">
                      Timestamp
                    </label>
                    <p class="mt-1 text-sm text-gray-900 dark:text-white">
                      {{ formatTimestamp(entry.timestamp) }}
                    </p>
                  </div>

                  <div>
                    <label class="text-xs font-medium uppercase text-gray-500 dark:text-gray-400">
                      Category
                    </label>
                    <p class="mt-1 text-sm text-gray-900 dark:text-white">
                      {{ getCategoryLabel(entry.category) }}
                    </p>
                  </div>

                  <div>
                    <label class="text-xs font-medium uppercase text-gray-500 dark:text-gray-400">
                      Severity
                    </label>
                    <p class="mt-1">
                      <span
                        class="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium"
                        :class="getSeverityClasses(getSeverityColor(entry.severity))"
                      >
                        {{ entry.severity }}
                      </span>
                    </p>
                  </div>

                  <div>
                    <label class="text-xs font-medium uppercase text-gray-500 dark:text-gray-400">
                      User
                    </label>
                    <p class="mt-1 text-sm text-gray-900 dark:text-white">
                      {{ entry.username || 'System' }}
                    </p>
                  </div>

                  <div v-if="entry.resourceType">
                    <label class="text-xs font-medium uppercase text-gray-500 dark:text-gray-400">
                      Resource Type
                    </label>
                    <p class="mt-1 text-sm text-gray-900 dark:text-white">
                      {{ entry.resourceType }}
                    </p>
                  </div>

                  <div v-if="entry.resourceId">
                    <label class="text-xs font-medium uppercase text-gray-500 dark:text-gray-400">
                      Resource ID
                    </label>
                    <div class="mt-1 flex items-center gap-2">
                      <code class="text-sm text-gray-900 dark:text-white">
                        {{ entry.resourceId }}
                      </code>
                      <button
                        class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
                        @click="copyToClipboard(entry.resourceId!)"
                      >
                        <ClipboardDocumentIcon class="h-4 w-4" />
                      </button>
                    </div>
                  </div>

                  <div v-if="entry.ipAddress">
                    <label class="text-xs font-medium uppercase text-gray-500 dark:text-gray-400">
                      IP Address
                    </label>
                    <p class="mt-1 text-sm text-gray-900 dark:text-white">
                      {{ entry.ipAddress }}
                    </p>
                  </div>

                  <div v-if="entry.correlationId">
                    <label class="text-xs font-medium uppercase text-gray-500 dark:text-gray-400">
                      Correlation ID
                    </label>
                    <div class="mt-1 flex items-center gap-2">
                      <code class="truncate text-sm text-gray-900 dark:text-white">
                        {{ entry.correlationId }}
                      </code>
                      <button
                        class="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
                        @click="copyToClipboard(entry.correlationId!)"
                      >
                        <ClipboardDocumentIcon class="h-4 w-4" />
                      </button>
                    </div>
                  </div>
                </div>

                <!-- Error Message -->
                <div
                  v-if="!entry.success && entry.errorMessage"
                  class="mt-6 rounded-lg bg-red-50 p-4 dark:bg-red-900/20"
                >
                  <label class="text-xs font-medium uppercase text-red-700 dark:text-red-300">
                    Error Message
                  </label>
                  <p class="mt-1 text-sm text-red-800 dark:text-red-200">
                    {{ entry.errorMessage }}
                  </p>
                </div>

                <!-- User Agent -->
                <div v-if="entry.userAgent" class="mt-6">
                  <label class="text-xs font-medium uppercase text-gray-500 dark:text-gray-400">
                    User Agent
                  </label>
                  <p class="mt-1 text-sm text-gray-600 dark:text-gray-300">
                    {{ entry.userAgent }}
                  </p>
                </div>

                <!-- Metadata -->
                <div v-if="entry.metadata && Object.keys(entry.metadata).length > 0" class="mt-6">
                  <label class="text-xs font-medium uppercase text-gray-500 dark:text-gray-400">
                    Metadata
                  </label>
                  <pre
                    class="mt-1 overflow-x-auto rounded-lg bg-gray-100 p-3 text-sm dark:bg-gray-700"
                  >{{ JSON.stringify(entry.metadata, null, 2) }}</pre>
                </div>

                <!-- Old/New Values -->
                <div v-if="entry.oldValues || entry.newValues" class="mt-6 grid grid-cols-2 gap-4">
                  <div v-if="entry.oldValues">
                    <label class="text-xs font-medium uppercase text-gray-500 dark:text-gray-400">
                      Previous Values
                    </label>
                    <pre
                      class="mt-1 overflow-x-auto rounded-lg bg-red-50 p-3 text-sm dark:bg-red-900/20"
                    >{{ JSON.stringify(entry.oldValues, null, 2) }}</pre>
                  </div>
                  <div v-if="entry.newValues">
                    <label class="text-xs font-medium uppercase text-gray-500 dark:text-gray-400">
                      New Values
                    </label>
                    <pre
                      class="mt-1 overflow-x-auto rounded-lg bg-green-50 p-3 text-sm dark:bg-green-900/20"
                    >{{ JSON.stringify(entry.newValues, null, 2) }}</pre>
                  </div>
                </div>

                <!-- Entry ID -->
                <div class="mt-6 border-t pt-4 dark:border-gray-700">
                  <div class="flex items-center justify-between">
                    <label class="text-xs text-gray-500 dark:text-gray-400">
                      Entry ID: {{ entry.id }}
                    </label>
                    <button
                      class="text-xs text-aegis-600 hover:text-aegis-700 dark:text-aegis-400"
                      @click="copyToClipboard(entry.id)"
                    >
                      Copy ID
                    </button>
                  </div>
                </div>
              </div>

              <!-- Footer -->
              <div class="flex justify-end border-t px-6 py-4 dark:border-gray-700">
                <button type="button" class="btn-secondary" @click="emit('close')">
                  Close
                </button>
              </div>
            </DialogPanel>
          </TransitionChild>
        </div>
      </div>
    </Dialog>
  </TransitionRoot>
</template>
