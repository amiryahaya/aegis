<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import {
  Dialog,
  DialogPanel,
  DialogTitle,
  TransitionRoot,
  TransitionChild,
  Disclosure,
  DisclosureButton,
  DisclosurePanel
} from '@headlessui/vue'
import {
  XMarkIcon,
  GlobeAltIcon,
  ChevronDownIcon,
  PlusIcon,
  TrashIcon
} from '@heroicons/vue/24/outline'
import type { WebhookSubscription, CreateWebhookRequest, UpdateWebhookRequest, WebhookEventType } from '@/types'
import { WEBHOOK_EVENT_CATEGORIES, getEventLabel } from '@/types/webhook'

const props = defineProps<{
  isOpen: boolean
  webhook?: WebhookSubscription | null
  isLoading?: boolean
}>()

const emit = defineEmits<{
  close: []
  create: [request: CreateWebhookRequest]
  update: [id: string, request: UpdateWebhookRequest]
}>()

const isEditMode = computed(() => !!props.webhook)
const dialogTitle = computed(() => (isEditMode.value ? 'Edit Webhook' : 'Create Webhook'))

// Form state
const name = ref('')
const url = ref('')
const description = ref('')
const secret = ref('')
const isActive = ref(true)
const selectedEvents = ref<WebhookEventType[]>([])
const headers = ref<Array<{ key: string; value: string }>>([])

// Validation
const errors = ref<Record<string, string>>({})

function validate(): boolean {
  errors.value = {}

  if (!name.value.trim()) {
    errors.value.name = 'Name is required'
  }

  if (!url.value.trim()) {
    errors.value.url = 'URL is required'
  } else {
    try {
      new URL(url.value)
    } catch {
      errors.value.url = 'Please enter a valid URL'
    }
  }

  if (selectedEvents.value.length === 0) {
    errors.value.events = 'Select at least one event'
  }

  return Object.keys(errors.value).length === 0
}

function toggleEvent(event: WebhookEventType) {
  const index = selectedEvents.value.indexOf(event)
  if (index === -1) {
    selectedEvents.value.push(event)
  } else {
    selectedEvents.value.splice(index, 1)
  }
}

function toggleCategory(events: WebhookEventType[]) {
  const allSelected = events.every(e => selectedEvents.value.includes(e))
  if (allSelected) {
    // Remove all events in this category
    selectedEvents.value = selectedEvents.value.filter(e => !events.includes(e))
  } else {
    // Add all events in this category
    events.forEach(e => {
      if (!selectedEvents.value.includes(e)) {
        selectedEvents.value.push(e)
      }
    })
  }
}

function addHeader() {
  headers.value.push({ key: '', value: '' })
}

function removeHeader(index: number) {
  headers.value.splice(index, 1)
}

function handleSubmit() {
  if (!validate()) return

  const headersObj: Record<string, string> = {}
  headers.value.forEach(h => {
    if (h.key.trim()) {
      headersObj[h.key.trim()] = h.value
    }
  })

  if (isEditMode.value && props.webhook) {
    const request: UpdateWebhookRequest = {
      name: name.value,
      url: url.value,
      description: description.value || undefined,
      events: selectedEvents.value,
      isActive: isActive.value,
      headers: Object.keys(headersObj).length > 0 ? headersObj : undefined
    }
    if (secret.value) {
      request.secret = secret.value
    }
    emit('update', props.webhook.id, request)
  } else {
    const request: CreateWebhookRequest = {
      name: name.value,
      url: url.value,
      description: description.value || undefined,
      events: selectedEvents.value,
      isActive: isActive.value,
      headers: Object.keys(headersObj).length > 0 ? headersObj : undefined
    }
    if (secret.value) {
      request.secret = secret.value
    }
    emit('create', request)
  }
}

function resetForm() {
  name.value = ''
  url.value = ''
  description.value = ''
  secret.value = ''
  isActive.value = true
  selectedEvents.value = []
  headers.value = []
  errors.value = {}
}

// Watch for webhook changes (edit mode)
watch(
  () => props.webhook,
  webhook => {
    if (webhook) {
      name.value = webhook.name
      url.value = webhook.url
      description.value = webhook.description || ''
      isActive.value = webhook.isActive
      selectedEvents.value = [...webhook.events]
      headers.value = Object.entries(webhook.headers).map(([key, value]) => ({
        key,
        value
      }))
      secret.value = ''
    } else {
      resetForm()
    }
  },
  { immediate: true }
)

// Reset form when dialog closes
watch(
  () => props.isOpen,
  isOpen => {
    if (!isOpen) {
      setTimeout(resetForm, 300)
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
              class="w-full max-w-lg transform overflow-hidden rounded-xl bg-white shadow-xl transition-all dark:bg-gray-800"
            >
              <!-- Header -->
              <div class="flex items-center justify-between border-b px-6 py-4 dark:border-gray-700">
                <div class="flex items-center gap-3">
                  <div class="rounded-lg bg-aegis-100 p-2 dark:bg-aegis-900/30">
                    <GlobeAltIcon class="h-5 w-5 text-aegis-600 dark:text-aegis-400" />
                  </div>
                  <DialogTitle class="text-lg font-semibold text-gray-900 dark:text-white">
                    {{ dialogTitle }}
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

              <!-- Form -->
              <form class="max-h-[70vh] overflow-y-auto p-6" @submit.prevent="handleSubmit">
                <div class="space-y-4">
                  <!-- Name -->
                  <div>
                    <label
                      for="webhook-name"
                      class="block text-sm font-medium text-gray-700 dark:text-gray-300"
                    >
                      Name *
                    </label>
                    <input
                      id="webhook-name"
                      v-model="name"
                      type="text"
                      placeholder="My Webhook"
                      class="mt-1 block w-full rounded-lg border-gray-300 shadow-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white sm:text-sm"
                      :class="{ 'border-red-500': errors.name }"
                    />
                    <p v-if="errors.name" class="mt-1 text-sm text-red-600">
                      {{ errors.name }}
                    </p>
                  </div>

                  <!-- URL -->
                  <div>
                    <label
                      for="webhook-url"
                      class="block text-sm font-medium text-gray-700 dark:text-gray-300"
                    >
                      URL *
                    </label>
                    <input
                      id="webhook-url"
                      v-model="url"
                      type="url"
                      placeholder="https://example.com/webhook"
                      class="mt-1 block w-full rounded-lg border-gray-300 shadow-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white sm:text-sm"
                      :class="{ 'border-red-500': errors.url }"
                    />
                    <p v-if="errors.url" class="mt-1 text-sm text-red-600">
                      {{ errors.url }}
                    </p>
                  </div>

                  <!-- Description -->
                  <div>
                    <label
                      for="webhook-description"
                      class="block text-sm font-medium text-gray-700 dark:text-gray-300"
                    >
                      Description
                    </label>
                    <textarea
                      id="webhook-description"
                      v-model="description"
                      rows="2"
                      placeholder="What is this webhook for?"
                      class="mt-1 block w-full rounded-lg border-gray-300 shadow-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white sm:text-sm"
                    />
                  </div>

                  <!-- Events -->
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">
                      Events *
                    </label>
                    <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
                      Select the events that will trigger this webhook
                    </p>
                    <p v-if="errors.events" class="mt-1 text-sm text-red-600">
                      {{ errors.events }}
                    </p>

                    <div class="mt-2 space-y-2">
                      <Disclosure
                        v-for="category in WEBHOOK_EVENT_CATEGORIES"
                        :key="category.name"
                        v-slot="{ open }"
                        as="div"
                        class="rounded-lg border dark:border-gray-700"
                      >
                        <DisclosureButton
                          class="flex w-full items-center justify-between px-3 py-2 text-left text-sm font-medium text-gray-900 hover:bg-gray-50 dark:text-white dark:hover:bg-gray-700"
                        >
                          <div class="flex items-center gap-2">
                            <input
                              type="checkbox"
                              :checked="category.events.every(e => selectedEvents.includes(e))"
                              :indeterminate="
                                category.events.some(e => selectedEvents.includes(e)) &&
                                !category.events.every(e => selectedEvents.includes(e))
                              "
                              class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500"
                              @click.stop="toggleCategory(category.events)"
                            />
                            <span>{{ category.name }}</span>
                            <span class="text-xs text-gray-500 dark:text-gray-400">
                              ({{
                                category.events.filter(e => selectedEvents.includes(e)).length
                              }}/{{ category.events.length }})
                            </span>
                          </div>
                          <ChevronDownIcon
                            class="h-4 w-4 transition-transform"
                            :class="{ 'rotate-180': open }"
                          />
                        </DisclosureButton>
                        <DisclosurePanel class="border-t px-3 py-2 dark:border-gray-700">
                          <div class="space-y-1">
                            <label
                              v-for="event in category.events"
                              :key="event"
                              class="flex items-center gap-2 rounded p-1 hover:bg-gray-50 dark:hover:bg-gray-700"
                            >
                              <input
                                type="checkbox"
                                :checked="selectedEvents.includes(event)"
                                class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500"
                                @change="toggleEvent(event)"
                              />
                              <span class="text-sm text-gray-700 dark:text-gray-300">
                                {{ getEventLabel(event) }}
                              </span>
                            </label>
                          </div>
                        </DisclosurePanel>
                      </Disclosure>
                    </div>
                  </div>

                  <!-- Secret -->
                  <div>
                    <label
                      for="webhook-secret"
                      class="block text-sm font-medium text-gray-700 dark:text-gray-300"
                    >
                      Secret
                      <span v-if="isEditMode" class="text-gray-500">(leave blank to keep current)</span>
                    </label>
                    <input
                      id="webhook-secret"
                      v-model="secret"
                      type="password"
                      placeholder="Optional signing secret"
                      class="mt-1 block w-full rounded-lg border-gray-300 shadow-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white sm:text-sm"
                    />
                    <p class="mt-1 text-xs text-gray-500 dark:text-gray-400">
                      Used to sign webhook payloads with HMAC-SHA256
                    </p>
                  </div>

                  <!-- Headers -->
                  <div>
                    <div class="flex items-center justify-between">
                      <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">
                        Custom Headers
                      </label>
                      <button
                        type="button"
                        class="inline-flex items-center gap-1 text-sm text-aegis-600 hover:text-aegis-700 dark:text-aegis-400"
                        @click="addHeader"
                      >
                        <PlusIcon class="h-4 w-4" />
                        Add Header
                      </button>
                    </div>
                    <div v-if="headers.length > 0" class="mt-2 space-y-2">
                      <div
                        v-for="(header, index) in headers"
                        :key="index"
                        class="flex items-center gap-2"
                      >
                        <input
                          v-model="header.key"
                          type="text"
                          placeholder="Header name"
                          class="block w-1/3 rounded-lg border-gray-300 shadow-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white sm:text-sm"
                        />
                        <input
                          v-model="header.value"
                          type="text"
                          placeholder="Header value"
                          class="block flex-1 rounded-lg border-gray-300 shadow-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white sm:text-sm"
                        />
                        <button
                          type="button"
                          class="rounded p-1 text-gray-400 hover:bg-gray-100 hover:text-red-500 dark:hover:bg-gray-700"
                          @click="removeHeader(index)"
                        >
                          <TrashIcon class="h-4 w-4" />
                        </button>
                      </div>
                    </div>
                  </div>

                  <!-- Active -->
                  <div class="flex items-center gap-3">
                    <input
                      id="webhook-active"
                      v-model="isActive"
                      type="checkbox"
                      class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500"
                    />
                    <label
                      for="webhook-active"
                      class="text-sm font-medium text-gray-700 dark:text-gray-300"
                    >
                      Active
                    </label>
                  </div>
                </div>

                <!-- Actions -->
                <div class="mt-6 flex justify-end gap-3">
                  <button
                    type="button"
                    class="btn-secondary"
                    :disabled="isLoading"
                    @click="emit('close')"
                  >
                    Cancel
                  </button>
                  <button type="submit" class="btn-primary" :disabled="isLoading">
                    <span v-if="isLoading">Saving...</span>
                    <span v-else>{{ isEditMode ? 'Save Changes' : 'Create Webhook' }}</span>
                  </button>
                </div>
              </form>
            </DialogPanel>
          </TransitionChild>
        </div>
      </div>
    </Dialog>
  </TransitionRoot>
</template>
