<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { TabGroup, TabList, Tab, TabPanels, TabPanel, Listbox, ListboxButton, ListboxOptions, ListboxOption } from '@headlessui/vue'
import {
  ClipboardDocumentListIcon,
  MagnifyingGlassIcon,
  FunnelIcon,
  ArrowDownTrayIcon,
  ArrowPathIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  ChevronUpDownIcon,
  CheckIcon,
  XMarkIcon
} from '@heroicons/vue/24/outline'
import { useAuditStore } from '@/stores/audit'
import { useToast } from '@/composables/useToast'
import AuditLogTable from '@/components/audit/AuditLogTable.vue'
import AuditLogDetail from '@/components/audit/AuditLogDetail.vue'
import SystemHealthCard from '@/components/audit/SystemHealthCard.vue'
import type { DetailedAuditLogEntry, AuditCategory, AuditSeverity } from '@/types'
import { AUDIT_CATEGORIES, SEVERITY_LEVELS } from '@/types/audit'

const auditStore = useAuditStore()
const toast = useToast()

// State
const selectedTab = ref(0)
const searchQuery = ref('')
const selectedCategories = ref<AuditCategory[]>([])
const selectedSeverity = ref<AuditSeverity | null>(null)
const successFilter = ref<boolean | null>(null)
const isDetailOpen = ref(false)
const selectedEntry = ref<DetailedAuditLogEntry | null>(null)
const showFilters = ref(false)

// Computed
const filteredEntries = computed(() => {
  let result = auditStore.entries

  if (searchQuery.value) {
    const search = searchQuery.value.toLowerCase()
    result = result.filter(
      e =>
        e.description?.toLowerCase().includes(search) ||
        e.username?.toLowerCase().includes(search) ||
        e.action.toLowerCase().includes(search) ||
        e.resourceId?.toLowerCase().includes(search)
    )
  }

  if (selectedCategories.value.length > 0) {
    result = result.filter(e => selectedCategories.value.includes(e.category))
  }

  if (selectedSeverity.value) {
    const severityOrder: AuditSeverity[] = ['Debug', 'Info', 'Warning', 'Error', 'Critical']
    const minIndex = severityOrder.indexOf(selectedSeverity.value)
    result = result.filter(e => severityOrder.indexOf(e.severity) >= minIndex)
  }

  if (successFilter.value !== null) {
    result = result.filter(e => e.success === successFilter.value)
  }

  return result
})

const stats = computed(() => auditStore.statistics)

const hasActiveFilters = computed(() => {
  return (
    selectedCategories.value.length > 0 ||
    selectedSeverity.value !== null ||
    successFilter.value !== null
  )
})

// Actions
async function loadData() {
  try {
    await Promise.all([
      auditStore.fetchEntries(),
      auditStore.fetchStatistics(),
      auditStore.fetchSystemHealth()
    ])
  } catch {
    toast.error('Failed to load audit data')
  }
}

function handleSort(field: string) {
  const isDescending = auditStore.sortBy === field ? !auditStore.sortDescending : true
  auditStore.setSorting(field, isDescending)
}

function handleSelectEntry(entry: DetailedAuditLogEntry) {
  selectedEntry.value = entry
  isDetailOpen.value = true
}

function clearFilters() {
  selectedCategories.value = []
  selectedSeverity.value = null
  successFilter.value = null
  searchQuery.value = ''
}

async function exportLogs(format: 'json' | 'csv') {
  try {
    const blob = await auditStore.exportLogs(format)
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `audit-logs-${new Date().toISOString().split('T')[0]}.${format}`
    a.click()
    URL.revokeObjectURL(url)
    toast.success(`Exported ${filteredEntries.value.length} entries`)
  } catch {
    toast.error('Failed to export logs')
  }
}

function goToPage(page: number) {
  auditStore.setPage(page)
}

// Lifecycle
onMounted(() => {
  loadData()
})
</script>

<template>
  <div class="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
    <!-- Header -->
    <div class="mb-6 flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Audit Log</h1>
        <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
          System activity and security events
        </p>
      </div>
      <div class="flex items-center gap-2">
        <button
          class="btn-secondary gap-2"
          @click="exportLogs('csv')"
        >
          <ArrowDownTrayIcon class="h-4 w-4" />
          Export CSV
        </button>
        <button
          class="btn-secondary gap-2"
          @click="exportLogs('json')"
        >
          <ArrowDownTrayIcon class="h-4 w-4" />
          Export JSON
        </button>
      </div>
    </div>

    <!-- Tabs -->
    <TabGroup :selected-index="selectedTab" @change="selectedTab = $event">
      <TabList class="flex gap-4 border-b dark:border-gray-700">
        <Tab v-slot="{ selected }" as="template">
          <button
            class="flex items-center gap-2 border-b-2 px-1 py-3 text-sm font-medium transition-colors"
            :class="[
              selected
                ? 'border-aegis-500 text-aegis-600 dark:text-aegis-400'
                : 'border-transparent text-gray-500 hover:border-gray-300 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-300'
            ]"
          >
            <ClipboardDocumentListIcon class="h-5 w-5" />
            Activity Log
            <span
              v-if="filteredEntries.length > 0"
              class="rounded-full bg-gray-100 px-2 py-0.5 text-xs dark:bg-gray-700"
            >
              {{ filteredEntries.length }}
            </span>
          </button>
        </Tab>
        <Tab v-slot="{ selected }" as="template">
          <button
            class="flex items-center gap-2 border-b-2 px-1 py-3 text-sm font-medium transition-colors"
            :class="[
              selected
                ? 'border-aegis-500 text-aegis-600 dark:text-aegis-400'
                : 'border-transparent text-gray-500 hover:border-gray-300 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-300'
            ]"
          >
            System Health
          </button>
        </Tab>
      </TabList>

      <TabPanels class="mt-6">
        <!-- Activity Log Tab -->
        <TabPanel>
          <!-- Stats Overview -->
          <div v-if="stats" class="mb-6 grid grid-cols-2 gap-4 sm:grid-cols-4">
            <div class="rounded-lg border bg-white p-4 dark:border-gray-700 dark:bg-gray-800">
              <p class="text-2xl font-bold text-gray-900 dark:text-white">
                {{ stats.totalEvents.toLocaleString() }}
              </p>
              <p class="text-sm text-gray-500 dark:text-gray-400">Total Events</p>
            </div>
            <div class="rounded-lg border bg-white p-4 dark:border-gray-700 dark:bg-gray-800">
              <p class="text-2xl font-bold text-green-600 dark:text-green-400">
                {{ stats.successfulEvents.toLocaleString() }}
              </p>
              <p class="text-sm text-gray-500 dark:text-gray-400">Successful</p>
            </div>
            <div class="rounded-lg border bg-white p-4 dark:border-gray-700 dark:bg-gray-800">
              <p class="text-2xl font-bold text-red-600 dark:text-red-400">
                {{ stats.failedEvents.toLocaleString() }}
              </p>
              <p class="text-sm text-gray-500 dark:text-gray-400">Failed</p>
            </div>
            <div class="rounded-lg border bg-white p-4 dark:border-gray-700 dark:bg-gray-800">
              <p class="text-2xl font-bold text-gray-900 dark:text-white">
                {{ stats.uniqueUsers }}
              </p>
              <p class="text-sm text-gray-500 dark:text-gray-400">Unique Users</p>
            </div>
          </div>

          <!-- Filters -->
          <div class="mb-4 space-y-4">
            <div class="flex flex-wrap items-center gap-4">
              <!-- Search -->
              <div class="relative flex-1">
                <MagnifyingGlassIcon
                  class="absolute left-3 top-1/2 h-5 w-5 -translate-y-1/2 text-gray-400"
                />
                <input
                  v-model="searchQuery"
                  type="text"
                  placeholder="Search logs..."
                  class="w-full rounded-lg border-gray-300 pl-10 focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                />
              </div>

              <!-- Filter Toggle -->
              <button
                class="btn-secondary gap-2"
                :class="{ 'bg-aegis-100 dark:bg-aegis-900/30': hasActiveFilters }"
                @click="showFilters = !showFilters"
              >
                <FunnelIcon class="h-5 w-5" />
                Filters
                <span
                  v-if="hasActiveFilters"
                  class="rounded-full bg-aegis-500 px-1.5 py-0.5 text-xs text-white"
                >
                  {{ (selectedCategories.length > 0 ? 1 : 0) + (selectedSeverity ? 1 : 0) + (successFilter !== null ? 1 : 0) }}
                </span>
              </button>

              <!-- Refresh -->
              <button
                class="rounded-lg p-2 text-gray-400 hover:bg-gray-100 hover:text-gray-600 dark:hover:bg-gray-700"
                :disabled="auditStore.isLoading"
                @click="loadData"
              >
                <ArrowPathIcon
                  class="h-5 w-5"
                  :class="{ 'animate-spin': auditStore.isLoading }"
                />
              </button>
            </div>

            <!-- Expanded Filters -->
            <div
              v-if="showFilters"
              class="flex flex-wrap items-center gap-4 rounded-lg border bg-gray-50 p-4 dark:border-gray-700 dark:bg-gray-800/50"
            >
              <!-- Category Filter -->
              <Listbox v-model="selectedCategories" multiple>
                <div class="relative">
                  <ListboxButton
                    class="relative w-48 cursor-pointer rounded-lg border border-gray-300 bg-white py-2 pl-3 pr-10 text-left text-sm focus:border-aegis-500 focus:outline-none focus:ring-1 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                  >
                    <span class="block truncate">
                      {{ selectedCategories.length === 0 ? 'All Categories' : `${selectedCategories.length} selected` }}
                    </span>
                    <span class="pointer-events-none absolute inset-y-0 right-0 flex items-center pr-2">
                      <ChevronUpDownIcon class="h-5 w-5 text-gray-400" />
                    </span>
                  </ListboxButton>
                  <transition
                    leave-active-class="transition duration-100 ease-in"
                    leave-from-class="opacity-100"
                    leave-to-class="opacity-0"
                  >
                    <ListboxOptions
                      class="absolute z-10 mt-1 max-h-60 w-full overflow-auto rounded-lg bg-white py-1 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-800"
                    >
                      <ListboxOption
                        v-for="category in AUDIT_CATEGORIES"
                        :key="category.name"
                        v-slot="{ active, selected }"
                        :value="category.name"
                      >
                        <li
                          class="relative cursor-pointer select-none py-2 pl-10 pr-4 text-sm"
                          :class="[
                            active ? 'bg-aegis-100 text-aegis-900 dark:bg-aegis-900/30' : 'text-gray-900 dark:text-white'
                          ]"
                        >
                          <span :class="['block truncate', selected ? 'font-medium' : 'font-normal']">
                            {{ category.label }}
                          </span>
                          <span
                            v-if="selected"
                            class="absolute inset-y-0 left-0 flex items-center pl-3 text-aegis-600"
                          >
                            <CheckIcon class="h-5 w-5" />
                          </span>
                        </li>
                      </ListboxOption>
                    </ListboxOptions>
                  </transition>
                </div>
              </Listbox>

              <!-- Severity Filter -->
              <Listbox v-model="selectedSeverity">
                <div class="relative">
                  <ListboxButton
                    class="relative w-40 cursor-pointer rounded-lg border border-gray-300 bg-white py-2 pl-3 pr-10 text-left text-sm focus:border-aegis-500 focus:outline-none focus:ring-1 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
                  >
                    <span class="block truncate">
                      {{ selectedSeverity || 'All Severity' }}
                    </span>
                    <span class="pointer-events-none absolute inset-y-0 right-0 flex items-center pr-2">
                      <ChevronUpDownIcon class="h-5 w-5 text-gray-400" />
                    </span>
                  </ListboxButton>
                  <transition
                    leave-active-class="transition duration-100 ease-in"
                    leave-from-class="opacity-100"
                    leave-to-class="opacity-0"
                  >
                    <ListboxOptions
                      class="absolute z-10 mt-1 max-h-60 w-full overflow-auto rounded-lg bg-white py-1 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-800"
                    >
                      <ListboxOption
                        v-slot="{ active }"
                        :value="null"
                      >
                        <li
                          class="relative cursor-pointer select-none py-2 pl-10 pr-4 text-sm"
                          :class="[active ? 'bg-aegis-100 dark:bg-aegis-900/30' : '', 'text-gray-900 dark:text-white']"
                        >
                          All Severity
                        </li>
                      </ListboxOption>
                      <ListboxOption
                        v-for="level in SEVERITY_LEVELS"
                        :key="level.value"
                        v-slot="{ active, selected }"
                        :value="level.value"
                      >
                        <li
                          class="relative cursor-pointer select-none py-2 pl-10 pr-4 text-sm"
                          :class="[
                            active ? 'bg-aegis-100 text-aegis-900 dark:bg-aegis-900/30' : 'text-gray-900 dark:text-white'
                          ]"
                        >
                          <span :class="['block truncate', selected ? 'font-medium' : 'font-normal']">
                            {{ level.label }}+
                          </span>
                          <span
                            v-if="selected"
                            class="absolute inset-y-0 left-0 flex items-center pl-3 text-aegis-600"
                          >
                            <CheckIcon class="h-5 w-5" />
                          </span>
                        </li>
                      </ListboxOption>
                    </ListboxOptions>
                  </transition>
                </div>
              </Listbox>

              <!-- Status Filter -->
              <select
                v-model="successFilter"
                class="rounded-lg border-gray-300 text-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
              >
                <option :value="null">All Status</option>
                <option :value="true">Success Only</option>
                <option :value="false">Failed Only</option>
              </select>

              <!-- Clear Filters -->
              <button
                v-if="hasActiveFilters"
                class="flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-300"
                @click="clearFilters"
              >
                <XMarkIcon class="h-4 w-4" />
                Clear
              </button>
            </div>
          </div>

          <!-- Table -->
          <AuditLogTable
            :entries="filteredEntries"
            :is-loading="auditStore.isLoading"
            :sort-by="auditStore.sortBy"
            :sort-descending="auditStore.sortDescending"
            @sort="handleSort"
            @select="handleSelectEntry"
          />

          <!-- Pagination -->
          <div
            v-if="auditStore.totalPages > 1"
            class="mt-4 flex items-center justify-between"
          >
            <p class="text-sm text-gray-500 dark:text-gray-400">
              Showing {{ (auditStore.page - 1) * auditStore.pageSize + 1 }} to
              {{ Math.min(auditStore.page * auditStore.pageSize, auditStore.totalCount) }}
              of {{ auditStore.totalCount }} entries
            </p>
            <div class="flex items-center gap-2">
              <button
                class="rounded-lg border p-2 hover:bg-gray-100 disabled:opacity-50 dark:border-gray-700 dark:hover:bg-gray-800"
                :disabled="!auditStore.hasPreviousPage"
                @click="goToPage(auditStore.page - 1)"
              >
                <ChevronLeftIcon class="h-5 w-5" />
              </button>
              <span class="text-sm text-gray-700 dark:text-gray-300">
                Page {{ auditStore.page }} of {{ auditStore.totalPages }}
              </span>
              <button
                class="rounded-lg border p-2 hover:bg-gray-100 disabled:opacity-50 dark:border-gray-700 dark:hover:bg-gray-800"
                :disabled="!auditStore.hasNextPage"
                @click="goToPage(auditStore.page + 1)"
              >
                <ChevronRightIcon class="h-5 w-5" />
              </button>
            </div>
          </div>
        </TabPanel>

        <!-- System Health Tab -->
        <TabPanel>
          <div class="grid gap-6 lg:grid-cols-2">
            <SystemHealthCard
              :health="auditStore.systemHealth"
              :is-loading="auditStore.isLoadingHealth"
              @refresh="auditStore.fetchSystemHealth"
            />

            <!-- Statistics Summary -->
            <div
              v-if="stats"
              class="rounded-lg border bg-white p-6 dark:border-gray-700 dark:bg-gray-800"
            >
              <h3 class="text-lg font-semibold text-gray-900 dark:text-white">
                Event Statistics (7 days)
              </h3>

              <!-- Category Breakdown -->
              <div class="mt-4">
                <h4 class="text-sm font-medium text-gray-700 dark:text-gray-300">
                  By Category
                </h4>
                <div class="mt-2 space-y-2">
                  <div
                    v-for="(count, category) in stats.categoryBreakdown"
                    :key="category"
                    class="flex items-center justify-between"
                  >
                    <span class="text-sm text-gray-600 dark:text-gray-400">
                      {{ category }}
                    </span>
                    <div class="flex items-center gap-2">
                      <div class="h-2 w-24 overflow-hidden rounded-full bg-gray-200 dark:bg-gray-700">
                        <div
                          class="h-full bg-aegis-500"
                          :style="{ width: `${(count / stats.totalEvents) * 100}%` }"
                        />
                      </div>
                      <span class="text-sm font-medium text-gray-900 dark:text-white">
                        {{ count }}
                      </span>
                    </div>
                  </div>
                </div>
              </div>

              <!-- Severity Breakdown -->
              <div class="mt-6">
                <h4 class="text-sm font-medium text-gray-700 dark:text-gray-300">
                  By Severity
                </h4>
                <div class="mt-2 grid grid-cols-5 gap-2">
                  <div
                    v-for="(count, severity) in stats.severityBreakdown"
                    :key="severity"
                    class="rounded-lg border p-2 text-center dark:border-gray-700"
                  >
                    <p class="text-lg font-bold text-gray-900 dark:text-white">
                      {{ count }}
                    </p>
                    <p class="text-xs text-gray-500 dark:text-gray-400">
                      {{ severity }}
                    </p>
                  </div>
                </div>
              </div>

              <!-- Top Users -->
              <div class="mt-6">
                <h4 class="text-sm font-medium text-gray-700 dark:text-gray-300">
                  Top Users
                </h4>
                <div class="mt-2 space-y-2">
                  <div
                    v-for="(count, user) in stats.userBreakdown"
                    :key="user"
                    class="flex items-center justify-between"
                  >
                    <span class="text-sm text-gray-600 dark:text-gray-400">
                      {{ user }}
                    </span>
                    <span class="text-sm font-medium text-gray-900 dark:text-white">
                      {{ count }} events
                    </span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </TabPanel>
      </TabPanels>
    </TabGroup>

    <!-- Detail Dialog -->
    <AuditLogDetail
      :is-open="isDetailOpen"
      :entry="selectedEntry"
      @close="isDetailOpen = false"
    />
  </div>
</template>
