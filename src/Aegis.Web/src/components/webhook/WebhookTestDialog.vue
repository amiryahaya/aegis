<script setup lang="ts">
import { ref, watch } from 'vue'
import {
  Dialog,
  DialogPanel,
  DialogTitle,
  TransitionRoot,
  TransitionChild
} from '@headlessui/vue'
import {
  XMarkIcon,
  PlayIcon,
  CheckCircleIcon,
  XCircleIcon,
  ArrowPathIcon
} from '@heroicons/vue/24/outline'
import type { WebhookSubscription, WebhookTestResult } from '@/types'
import { formatDuration } from '@/types/webhook'

const props = defineProps<{
  isOpen: boolean
  webhook: WebhookSubscription | null
}>()

const emit = defineEmits<{
  close: []
  test: [webhookId: string]
}>()

const testResult = ref<WebhookTestResult | null>(null)
const isTesting = ref(false)

async function runTest() {
  if (!props.webhook) return

  isTesting.value = true
  testResult.value = null

  emit('test', props.webhook.id)
}

function handleTestResult(result: WebhookTestResult) {
  testResult.value = result
  isTesting.value = false
}

// Expose method to parent
defineExpose({ handleTestResult })

// Reset state when dialog closes
watch(
  () => props.isOpen,
  isOpen => {
    if (!isOpen) {
      setTimeout(() => {
        testResult.value = null
        isTesting.value = false
      }, 300)
    }
  }
)
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
              class="w-full max-w-md transform overflow-hidden rounded-xl bg-white shadow-xl transition-all dark:bg-gray-800"
            >
              <!-- Header -->
              <div class="flex items-center justify-between border-b px-6 py-4 dark:border-gray-700">
                <div class="flex items-center gap-3">
                  <div class="rounded-lg bg-aegis-100 p-2 dark:bg-aegis-900/30">
                    <PlayIcon class="h-5 w-5 text-aegis-600 dark:text-aegis-400" />
                  </div>
                  <DialogTitle class="text-lg font-semibold text-gray-900 dark:text-white">
                    Test Webhook
                  </DialogTitle>
                </div>
                <button
                  type="button"
                  class="rounded-lg p-1.5 text-gray-400 hover:bg-gray-100 hover:text-gray-500 dark:hover:bg-gray-700"
                  @click="emit('close')"
                >
                  <XMarkIcon class="h-5 w-5" />
                </button>
              </div>

              <!-- Content -->
              <div class="p-6">
                <!-- Webhook Info -->
                <div v-if="webhook" class="mb-6">
                  <h3 class="font-medium text-gray-900 dark:text-white">
                    {{ webhook.name }}
                  </h3>
                  <p class="mt-1 truncate text-sm text-gray-500 dark:text-gray-400">
                    {{ webhook.url }}
                  </p>
                </div>

                <!-- Test Info -->
                <div class="rounded-lg bg-gray-50 p-4 dark:bg-gray-700/50">
                  <p class="text-sm text-gray-600 dark:text-gray-300">
                    A test event will be sent to this webhook endpoint. The payload will include:
                  </p>
                  <ul class="mt-2 list-inside list-disc text-sm text-gray-500 dark:text-gray-400">
                    <li>Event type: <code class="text-aegis-600 dark:text-aegis-400">Test</code></li>
                    <li>Timestamp</li>
                    <li>Test payload with sample data</li>
                  </ul>
                </div>

                <!-- Testing State -->
                <div v-if="isTesting" class="mt-6 flex flex-col items-center py-4">
                  <ArrowPathIcon class="h-8 w-8 animate-spin text-aegis-500" />
                  <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">
                    Sending test event...
                  </p>
                </div>

                <!-- Test Result -->
                <div v-else-if="testResult" class="mt-6">
                  <div
                    class="flex items-start gap-3 rounded-lg p-4"
                    :class="{
                      'bg-green-50 dark:bg-green-900/20': testResult.success,
                      'bg-red-50 dark:bg-red-900/20': !testResult.success
                    }"
                  >
                    <CheckCircleIcon
                      v-if="testResult.success"
                      class="h-6 w-6 flex-shrink-0 text-green-500"
                    />
                    <XCircleIcon v-else class="h-6 w-6 flex-shrink-0 text-red-500" />
                    <div class="flex-1">
                      <p
                        class="font-medium"
                        :class="{
                          'text-green-800 dark:text-green-200': testResult.success,
                          'text-red-800 dark:text-red-200': !testResult.success
                        }"
                      >
                        {{ testResult.message }}
                      </p>
                      <div class="mt-2 space-y-1 text-sm">
                        <p class="text-gray-600 dark:text-gray-400">
                          <span class="font-medium">Status:</span>
                          {{ testResult.delivery.status }}
                        </p>
                        <p
                          v-if="testResult.delivery.httpStatusCode"
                          class="text-gray-600 dark:text-gray-400"
                        >
                          <span class="font-medium">HTTP Status:</span>
                          {{ testResult.delivery.httpStatusCode }}
                        </p>
                        <p class="text-gray-600 dark:text-gray-400">
                          <span class="font-medium">Duration:</span>
                          {{ formatDuration(testResult.delivery.duration) }}
                        </p>
                        <p
                          v-if="testResult.delivery.errorMessage"
                          class="text-red-600 dark:text-red-400"
                        >
                          <span class="font-medium">Error:</span>
                          {{ testResult.delivery.errorMessage }}
                        </p>
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              <!-- Actions -->
              <div class="flex justify-end gap-3 border-t px-6 py-4 dark:border-gray-700">
                <button type="button" class="btn-secondary" @click="emit('close')">
                  Close
                </button>
                <button
                  type="button"
                  class="btn-primary gap-2"
                  :disabled="isTesting"
                  @click="runTest"
                >
                  <PlayIcon class="h-4 w-4" />
                  {{ testResult ? 'Test Again' : 'Send Test Event' }}
                </button>
              </div>
            </DialogPanel>
          </TransitionChild>
        </div>
      </div>
    </Dialog>
  </TransitionRoot>
</template>
