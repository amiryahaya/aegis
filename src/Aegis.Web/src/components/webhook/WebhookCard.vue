<script setup lang="ts">
import { computed } from 'vue'
import {
  GlobeAltIcon,
  CheckCircleIcon,
  XCircleIcon,
  ExclamationTriangleIcon,
  EllipsisVerticalIcon,
  PlayIcon,
  PencilIcon,
  TrashIcon,
  ClockIcon
} from '@heroicons/vue/24/outline'
import { Menu, MenuButton, MenuItems, MenuItem } from '@headlessui/vue'
import type { WebhookSubscription } from '@/types'
import { getHealthStatus, getHealthColor, getEventLabel } from '@/types/webhook'

const props = defineProps<{
  webhook: WebhookSubscription
}>()

const emit = defineEmits<{
  test: [id: string]
  edit: [webhook: WebhookSubscription]
  delete: [id: string]
  toggle: [id: string]
  viewDetails: [id: string]
}>()

const healthStatus = computed(() => getHealthStatus(props.webhook.health))
const healthColor = computed(() => getHealthColor(healthStatus.value))

const healthIcon = computed(() => {
  switch (healthStatus.value) {
    case 'healthy':
      return CheckCircleIcon
    case 'degraded':
      return ExclamationTriangleIcon
    case 'unhealthy':
      return XCircleIcon
    default:
      return ClockIcon
  }
})

const displayedEvents = computed(() => {
  const events = props.webhook.events
  if (events.length <= 3) {
    return { shown: events.map(getEventLabel), remaining: 0 }
  }
  return {
    shown: events.slice(0, 2).map(getEventLabel),
    remaining: events.length - 2
  }
})

function formatDate(dateString: string): string {
  return new Date(dateString).toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    year: 'numeric'
  })
}
</script>

<template>
  <div
    class="rounded-lg border bg-white p-4 shadow-sm transition-shadow hover:shadow-md dark:border-gray-700 dark:bg-gray-800"
    :class="{ 'opacity-60': !webhook.isActive }"
  >
    <!-- Header -->
    <div class="flex items-start justify-between">
      <div class="flex items-center gap-3">
        <div
          class="rounded-lg p-2"
          :class="{
            'bg-green-100 dark:bg-green-900/30': healthColor === 'green',
            'bg-yellow-100 dark:bg-yellow-900/30': healthColor === 'yellow',
            'bg-red-100 dark:bg-red-900/30': healthColor === 'red',
            'bg-gray-100 dark:bg-gray-700': healthColor === 'gray'
          }"
        >
          <GlobeAltIcon
            class="h-5 w-5"
            :class="{
              'text-green-600 dark:text-green-400': healthColor === 'green',
              'text-yellow-600 dark:text-yellow-400': healthColor === 'yellow',
              'text-red-600 dark:text-red-400': healthColor === 'red',
              'text-gray-500 dark:text-gray-400': healthColor === 'gray'
            }"
          />
        </div>
        <div>
          <button
            class="font-medium text-gray-900 hover:text-aegis-600 dark:text-white dark:hover:text-aegis-400"
            @click="emit('viewDetails', webhook.id)"
          >
            {{ webhook.name }}
          </button>
          <div class="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400">
            <span
              class="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium"
              :class="{
                'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400':
                  webhook.isActive,
                'bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-400':
                  !webhook.isActive
              }"
            >
              {{ webhook.isActive ? 'Active' : 'Inactive' }}
            </span>
          </div>
        </div>
      </div>

      <!-- Actions Menu -->
      <Menu as="div" class="relative">
        <MenuButton
          class="rounded-lg p-1.5 text-gray-400 hover:bg-gray-100 hover:text-gray-500 dark:hover:bg-gray-700"
        >
          <EllipsisVerticalIcon class="h-5 w-5" />
        </MenuButton>
        <transition
          enter-active-class="transition duration-100 ease-out"
          enter-from-class="transform scale-95 opacity-0"
          enter-to-class="transform scale-100 opacity-100"
          leave-active-class="transition duration-75 ease-in"
          leave-from-class="transform scale-100 opacity-100"
          leave-to-class="transform scale-95 opacity-0"
        >
          <MenuItems
            class="absolute right-0 z-10 mt-2 w-48 origin-top-right rounded-lg bg-white py-1 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-800 dark:ring-gray-700"
          >
            <MenuItem v-slot="{ active }">
              <button
                class="flex w-full items-center gap-2 px-4 py-2 text-sm"
                :class="[
                  active ? 'bg-gray-100 dark:bg-gray-700' : '',
                  'text-gray-700 dark:text-gray-300'
                ]"
                @click="emit('test', webhook.id)"
              >
                <PlayIcon class="h-4 w-4" />
                Test Webhook
              </button>
            </MenuItem>
            <MenuItem v-slot="{ active }">
              <button
                class="flex w-full items-center gap-2 px-4 py-2 text-sm"
                :class="[
                  active ? 'bg-gray-100 dark:bg-gray-700' : '',
                  'text-gray-700 dark:text-gray-300'
                ]"
                @click="emit('edit', webhook)"
              >
                <PencilIcon class="h-4 w-4" />
                Edit
              </button>
            </MenuItem>
            <MenuItem v-slot="{ active }">
              <button
                class="flex w-full items-center gap-2 px-4 py-2 text-sm"
                :class="[
                  active ? 'bg-gray-100 dark:bg-gray-700' : '',
                  'text-gray-700 dark:text-gray-300'
                ]"
                @click="emit('toggle', webhook.id)"
              >
                {{ webhook.isActive ? 'Disable' : 'Enable' }}
              </button>
            </MenuItem>
            <MenuItem v-slot="{ active }">
              <button
                class="flex w-full items-center gap-2 px-4 py-2 text-sm text-red-600 dark:text-red-400"
                :class="[active ? 'bg-gray-100 dark:bg-gray-700' : '']"
                @click="emit('delete', webhook.id)"
              >
                <TrashIcon class="h-4 w-4" />
                Delete
              </button>
            </MenuItem>
          </MenuItems>
        </transition>
      </Menu>
    </div>

    <!-- URL -->
    <div class="mt-3">
      <p class="truncate text-sm text-gray-500 dark:text-gray-400" :title="webhook.url">
        {{ webhook.url }}
      </p>
    </div>

    <!-- Description -->
    <p
      v-if="webhook.description"
      class="mt-2 line-clamp-2 text-sm text-gray-600 dark:text-gray-300"
    >
      {{ webhook.description }}
    </p>

    <!-- Events -->
    <div class="mt-3 flex flex-wrap gap-1">
      <span
        v-for="event in displayedEvents.shown"
        :key="event"
        class="inline-flex items-center rounded-full bg-aegis-100 px-2 py-0.5 text-xs font-medium text-aegis-800 dark:bg-aegis-900/30 dark:text-aegis-300"
      >
        {{ event }}
      </span>
      <span
        v-if="displayedEvents.remaining > 0"
        class="inline-flex items-center rounded-full bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-600 dark:bg-gray-700 dark:text-gray-400"
      >
        +{{ displayedEvents.remaining }} more
      </span>
    </div>

    <!-- Health & Stats -->
    <div class="mt-4 flex items-center justify-between border-t pt-3 dark:border-gray-700">
      <div class="flex items-center gap-2">
        <component
          :is="healthIcon"
          class="h-4 w-4"
          :class="{
            'text-green-500': healthColor === 'green',
            'text-yellow-500': healthColor === 'yellow',
            'text-red-500': healthColor === 'red',
            'text-gray-400': healthColor === 'gray'
          }"
        />
        <span class="text-sm text-gray-600 dark:text-gray-400">
          {{ webhook.health.successRate.toFixed(1) }}% success
        </span>
        <span class="text-sm text-gray-400 dark:text-gray-500">
          ({{ webhook.health.successCount + webhook.health.failureCount }} deliveries)
        </span>
      </div>
      <span class="text-xs text-gray-400 dark:text-gray-500">
        Created {{ formatDate(webhook.createdAt) }}
      </span>
    </div>
  </div>
</template>
