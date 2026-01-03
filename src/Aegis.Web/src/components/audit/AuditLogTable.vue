<script setup lang="ts">
import {
  CheckCircleIcon,
  XCircleIcon,
  ChevronUpIcon,
  ChevronDownIcon
} from '@heroicons/vue/24/solid'
import { ClockIcon } from '@heroicons/vue/24/outline'
import type { DetailedAuditLogEntry } from '@/types'
import {
  getActionLabel,
  getSeverityColor,
  formatAuditRelativeTime
} from '@/types/audit'

defineProps<{
  entries: DetailedAuditLogEntry[]
  isLoading?: boolean
  sortBy?: string
  sortDescending?: boolean
}>()

const emit = defineEmits<{
  sort: [field: string]
  select: [entry: DetailedAuditLogEntry]
}>()

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

function getCategoryBadgeClasses(category: string): string {
  if (category.includes('Security')) {
    return 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400'
  }
  if (category.includes('Auth')) {
    return 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400'
  }
  if (category.includes('User') || category.includes('Team')) {
    return 'bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-400'
  }
  if (category.includes('Document') || category.includes('Data')) {
    return 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400'
  }
  if (category.includes('Query')) {
    return 'bg-aegis-100 text-aegis-800 dark:bg-aegis-900/30 dark:text-aegis-400'
  }
  return 'bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-400'
}

const columns = [
  { key: 'timestamp', label: 'Time', sortable: true },
  { key: 'action', label: 'Action', sortable: true },
  { key: 'category', label: 'Category', sortable: true },
  { key: 'username', label: 'User', sortable: true },
  { key: 'success', label: 'Status', sortable: true },
  { key: 'severity', label: 'Severity', sortable: true },
  { key: 'description', label: 'Description', sortable: false }
]
</script>

<template>
  <div class="overflow-hidden rounded-lg border dark:border-gray-700">
    <div class="overflow-x-auto">
      <table class="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
        <thead class="bg-gray-50 dark:bg-gray-800">
          <tr>
            <th
              v-for="col in columns"
              :key="col.key"
              scope="col"
              class="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400"
              :class="{ 'cursor-pointer hover:bg-gray-100 dark:hover:bg-gray-700': col.sortable }"
              @click="col.sortable && emit('sort', col.key)"
            >
              <div class="flex items-center gap-1">
                {{ col.label }}
                <template v-if="col.sortable && sortBy === col.key">
                  <ChevronUpIcon v-if="!sortDescending" class="h-4 w-4" />
                  <ChevronDownIcon v-else class="h-4 w-4" />
                </template>
              </div>
            </th>
          </tr>
        </thead>
        <tbody class="divide-y divide-gray-200 bg-white dark:divide-gray-700 dark:bg-gray-900">
          <!-- Loading state -->
          <template v-if="isLoading">
            <tr v-for="i in 10" :key="i">
              <td v-for="j in columns.length" :key="j" class="px-4 py-3">
                <div class="h-4 animate-pulse rounded bg-gray-200 dark:bg-gray-700" />
              </td>
            </tr>
          </template>

          <!-- Empty state -->
          <tr v-else-if="entries.length === 0">
            <td :colspan="columns.length" class="px-4 py-12 text-center">
              <ClockIcon class="mx-auto h-10 w-10 text-gray-300 dark:text-gray-600" />
              <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">No audit logs found</p>
            </td>
          </tr>

          <!-- Data rows -->
          <tr
            v-for="entry in entries"
            v-else
            :key="entry.id"
            class="cursor-pointer hover:bg-gray-50 dark:hover:bg-gray-800"
            @click="emit('select', entry)"
          >
            <!-- Timestamp -->
            <td class="whitespace-nowrap px-4 py-3 text-sm text-gray-500 dark:text-gray-400">
              {{ formatAuditRelativeTime(entry.timestamp) }}
            </td>

            <!-- Action -->
            <td class="whitespace-nowrap px-4 py-3">
              <span class="font-medium text-gray-900 dark:text-white">
                {{ getActionLabel(entry.action) }}
              </span>
            </td>

            <!-- Category -->
            <td class="whitespace-nowrap px-4 py-3">
              <span
                class="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium"
                :class="getCategoryBadgeClasses(entry.category)"
              >
                {{ entry.category }}
              </span>
            </td>

            <!-- User -->
            <td class="whitespace-nowrap px-4 py-3 text-sm text-gray-500 dark:text-gray-400">
              {{ entry.username || '-' }}
            </td>

            <!-- Status -->
            <td class="whitespace-nowrap px-4 py-3">
              <div class="flex items-center gap-1">
                <CheckCircleIcon
                  v-if="entry.success"
                  class="h-5 w-5 text-green-500 dark:text-green-400"
                />
                <XCircleIcon v-else class="h-5 w-5 text-red-500 dark:text-red-400" />
                <span
                  class="text-sm"
                  :class="
                    entry.success
                      ? 'text-green-700 dark:text-green-400'
                      : 'text-red-700 dark:text-red-400'
                  "
                >
                  {{ entry.success ? 'Success' : 'Failed' }}
                </span>
              </div>
            </td>

            <!-- Severity -->
            <td class="whitespace-nowrap px-4 py-3">
              <span
                class="inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium"
                :class="getSeverityClasses(getSeverityColor(entry.severity))"
              >
                {{ entry.severity }}
              </span>
            </td>

            <!-- Description -->
            <td class="max-w-xs truncate px-4 py-3 text-sm text-gray-500 dark:text-gray-400">
              {{ entry.description || '-' }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
