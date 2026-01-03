<script setup lang="ts">
import { computed } from 'vue'
import {
  CheckCircleIcon,
  XCircleIcon,
  ArrowPathIcon,
  ClockIcon,
  MinusCircleIcon
} from '@heroicons/vue/24/solid'
import type { WebhookDelivery } from '@/types'
import { getEventLabel, formatDuration, getDeliveryStatusColor } from '@/types/webhook'

defineProps<{
  deliveries: WebhookDelivery[]
  isLoading?: boolean
}>()

const emit = defineEmits<{
  retry: [deliveryId: string]
  viewDetails: [delivery: WebhookDelivery]
}>()

function getStatusIcon(status: WebhookDelivery['status']) {
  switch (status) {
    case 'Success':
      return CheckCircleIcon
    case 'Pending':
      return ClockIcon
    case 'Retrying':
      return ArrowPathIcon
    case 'Failed':
    case 'MaxRetriesExceeded':
      return XCircleIcon
    case 'Skipped':
      return MinusCircleIcon
    default:
      return ClockIcon
  }
}

function getStatusClasses(status: WebhookDelivery['status']) {
  const color = getDeliveryStatusColor(status)
  switch (color) {
    case 'green':
      return 'text-green-500 dark:text-green-400'
    case 'yellow':
      return 'text-yellow-500 dark:text-yellow-400'
    case 'red':
      return 'text-red-500 dark:text-red-400'
    default:
      return 'text-gray-500 dark:text-gray-400'
  }
}

function formatDate(dateString: string): string {
  const date = new Date(dateString)
  return date.toLocaleString(undefined, {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

const canRetry = (delivery: WebhookDelivery) => {
  return delivery.status === 'Failed' || delivery.status === 'MaxRetriesExceeded'
}
</script>

<template>
  <div class="overflow-hidden rounded-lg border dark:border-gray-700">
    <table class="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
      <thead class="bg-gray-50 dark:bg-gray-800">
        <tr>
          <th
            scope="col"
            class="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400"
          >
            Status
          </th>
          <th
            scope="col"
            class="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400"
          >
            Event
          </th>
          <th
            scope="col"
            class="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400"
          >
            Response
          </th>
          <th
            scope="col"
            class="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400"
          >
            Duration
          </th>
          <th
            scope="col"
            class="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400"
          >
            Time
          </th>
          <th scope="col" class="relative px-4 py-3">
            <span class="sr-only">Actions</span>
          </th>
        </tr>
      </thead>
      <tbody class="divide-y divide-gray-200 bg-white dark:divide-gray-700 dark:bg-gray-900">
        <!-- Loading state -->
        <template v-if="isLoading">
          <tr v-for="i in 5" :key="i">
            <td class="px-4 py-3">
              <div class="h-5 w-5 animate-pulse rounded-full bg-gray-200 dark:bg-gray-700" />
            </td>
            <td class="px-4 py-3">
              <div class="h-4 w-32 animate-pulse rounded bg-gray-200 dark:bg-gray-700" />
            </td>
            <td class="px-4 py-3">
              <div class="h-4 w-16 animate-pulse rounded bg-gray-200 dark:bg-gray-700" />
            </td>
            <td class="px-4 py-3">
              <div class="h-4 w-16 animate-pulse rounded bg-gray-200 dark:bg-gray-700" />
            </td>
            <td class="px-4 py-3">
              <div class="h-4 w-24 animate-pulse rounded bg-gray-200 dark:bg-gray-700" />
            </td>
            <td class="px-4 py-3" />
          </tr>
        </template>

        <!-- Empty state -->
        <tr v-else-if="deliveries.length === 0">
          <td colspan="6" class="px-4 py-8 text-center">
            <ClockIcon class="mx-auto h-10 w-10 text-gray-300 dark:text-gray-600" />
            <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">
              No delivery history yet
            </p>
          </td>
        </tr>

        <!-- Delivery rows -->
        <tr
          v-for="delivery in deliveries"
          v-else
          :key="delivery.id"
          class="hover:bg-gray-50 dark:hover:bg-gray-800"
        >
          <td class="whitespace-nowrap px-4 py-3">
            <div class="flex items-center gap-2">
              <component
                :is="getStatusIcon(delivery.status)"
                class="h-5 w-5"
                :class="getStatusClasses(delivery.status)"
              />
              <span class="text-sm text-gray-900 dark:text-white">
                {{ delivery.status }}
              </span>
              <span
                v-if="delivery.attemptNumber > 1"
                class="text-xs text-gray-500 dark:text-gray-400"
              >
                (Attempt {{ delivery.attemptNumber }})
              </span>
            </div>
          </td>
          <td class="whitespace-nowrap px-4 py-3">
            <span
              class="inline-flex items-center rounded-full bg-aegis-100 px-2 py-0.5 text-xs font-medium text-aegis-800 dark:bg-aegis-900/30 dark:text-aegis-300"
            >
              {{ getEventLabel(delivery.eventType) }}
            </span>
          </td>
          <td class="whitespace-nowrap px-4 py-3">
            <div class="flex items-center gap-2">
              <span
                v-if="delivery.httpStatusCode"
                class="inline-flex items-center rounded px-1.5 py-0.5 text-xs font-medium"
                :class="{
                  'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400':
                    delivery.httpStatusCode >= 200 && delivery.httpStatusCode < 300,
                  'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400':
                    delivery.httpStatusCode >= 400
                }"
              >
                {{ delivery.httpStatusCode }}
              </span>
              <span
                v-if="delivery.errorMessage"
                class="max-w-xs truncate text-xs text-red-600 dark:text-red-400"
                :title="delivery.errorMessage"
              >
                {{ delivery.errorMessage }}
              </span>
              <span v-else-if="!delivery.httpStatusCode" class="text-xs text-gray-400">
                -
              </span>
            </div>
          </td>
          <td class="whitespace-nowrap px-4 py-3 text-sm text-gray-500 dark:text-gray-400">
            {{ formatDuration(delivery.duration) }}
          </td>
          <td class="whitespace-nowrap px-4 py-3 text-sm text-gray-500 dark:text-gray-400">
            {{ formatDate(delivery.attemptedAt) }}
          </td>
          <td class="whitespace-nowrap px-4 py-3 text-right text-sm font-medium">
            <button
              v-if="canRetry(delivery)"
              class="text-aegis-600 hover:text-aegis-900 dark:text-aegis-400 dark:hover:text-aegis-300"
              @click="emit('retry', delivery.id)"
            >
              Retry
            </button>
            <button
              class="ml-4 text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-300"
              @click="emit('viewDetails', delivery)"
            >
              View
            </button>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>
