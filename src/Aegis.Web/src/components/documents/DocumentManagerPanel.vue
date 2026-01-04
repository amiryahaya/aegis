<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import type { DocumentResponse, DocumentType, DocumentStatus } from '@/services/document.service'
import {
  DocumentTextIcon,
  MagnifyingGlassIcon,
  FunnelIcon,
  ArrowsUpDownIcon,
  TrashIcon,
  ArrowDownTrayIcon,
  ArrowPathIcon,
  CheckIcon,
  ExclamationCircleIcon,
  ClockIcon,
  DocumentArrowUpIcon,
  EllipsisVerticalIcon,
  TableCellsIcon,
  PresentationChartBarIcon,
  CodeBracketIcon,
  PhotoIcon,
  DocumentIcon
} from '@heroicons/vue/24/outline'
import {
  Menu,
  MenuButton,
  MenuItems,
  MenuItem,
  Listbox,
  ListboxButton,
  ListboxOptions,
  ListboxOption
} from '@headlessui/vue'

const props = defineProps<{
  documents: DocumentResponse[]
  loading?: boolean
}>()

const emit = defineEmits<{
  (e: 'upload'): void
  (e: 'delete', id: string): void
  (e: 'bulkDelete', ids: string[]): void
  (e: 'reindex', id: string): void
  (e: 'download', id: string): void
  (e: 'viewDetails', doc: DocumentResponse): void
}>()

// Search and filter state
const searchQuery = ref('')
const selectedTypes = ref<DocumentType[]>([])
const selectedStatuses = ref<DocumentStatus[]>([])
const sortBy = ref<'name' | 'date' | 'size' | 'status'>('date')
const sortOrder = ref<'asc' | 'desc'>('desc')

// Selection state
const selectedIds = ref<Set<string>>(new Set())
const selectAll = ref(false)

// Document type options
const documentTypes: { value: DocumentType; label: string }[] = [
  { value: 'Pdf', label: 'PDF' },
  { value: 'Word', label: 'Word' },
  { value: 'Excel', label: 'Excel' },
  { value: 'PowerPoint', label: 'PowerPoint' },
  { value: 'Text', label: 'Text' },
  { value: 'Markdown', label: 'Markdown' },
  { value: 'Html', label: 'HTML' },
  { value: 'Json', label: 'JSON' },
  { value: 'Csv', label: 'CSV' },
  { value: 'Image', label: 'Image' }
]

// Status options
const statusOptions: { value: DocumentStatus; label: string; color: string }[] = [
  { value: 'Indexed', label: 'Indexed', color: 'green' },
  { value: 'Processing', label: 'Processing', color: 'blue' },
  { value: 'Pending', label: 'Pending', color: 'yellow' },
  { value: 'Failed', label: 'Failed', color: 'red' }
]

// Sort options
const sortOptions = [
  { value: 'name', label: 'Name' },
  { value: 'date', label: 'Date' },
  { value: 'size', label: 'Size' },
  { value: 'status', label: 'Status' }
]

// Filtered and sorted documents
const filteredDocuments = computed(() => {
  let result = [...props.documents]

  // Search filter
  if (searchQuery.value) {
    const query = searchQuery.value.toLowerCase()
    result = result.filter(doc =>
      doc.name.toLowerCase().includes(query)
    )
  }

  // Type filter
  if (selectedTypes.value.length > 0) {
    result = result.filter(doc => selectedTypes.value.includes(doc.type))
  }

  // Status filter
  if (selectedStatuses.value.length > 0) {
    result = result.filter(doc => selectedStatuses.value.includes(doc.status))
  }

  // Sort
  result.sort((a, b) => {
    let comparison = 0
    switch (sortBy.value) {
      case 'name':
        comparison = a.name.localeCompare(b.name)
        break
      case 'date':
        comparison = new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()
        break
      case 'size':
        comparison = a.size - b.size
        break
      case 'status':
        comparison = a.status.localeCompare(b.status)
        break
    }
    return sortOrder.value === 'desc' ? -comparison : comparison
  })

  return result
})

// Selection helpers
const isSelected = (id: string) => selectedIds.value.has(id)

const toggleSelection = (id: string) => {
  if (selectedIds.value.has(id)) {
    selectedIds.value.delete(id)
  } else {
    selectedIds.value.add(id)
  }
  selectedIds.value = new Set(selectedIds.value) // Trigger reactivity
}

const toggleSelectAll = () => {
  if (selectAll.value) {
    selectedIds.value = new Set()
    selectAll.value = false
  } else {
    selectedIds.value = new Set(filteredDocuments.value.map(d => d.id))
    selectAll.value = true
  }
}

watch(filteredDocuments, () => {
  // Update selectAll state when documents change
  selectAll.value = filteredDocuments.value.length > 0 &&
    filteredDocuments.value.every(d => selectedIds.value.has(d.id))
})

// Bulk actions
const handleBulkDelete = () => {
  if (selectedIds.value.size === 0) return
  emit('bulkDelete', Array.from(selectedIds.value))
  selectedIds.value = new Set()
  selectAll.value = false
}

// Formatting helpers
const formatFileSize = (bytes: number) => {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i]
}

const formatDate = (dateStr: string) => {
  return new Date(dateStr).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric'
  })
}

const getStatusIcon = (status: DocumentStatus) => {
  switch (status) {
    case 'Indexed':
      return CheckIcon
    case 'Processing':
      return ArrowPathIcon
    case 'Pending':
      return ClockIcon
    case 'Failed':
      return ExclamationCircleIcon
    default:
      return DocumentTextIcon
  }
}

const getStatusColor = (status: DocumentStatus) => {
  switch (status) {
    case 'Indexed':
      return 'text-green-500 bg-green-100 dark:bg-green-900/50'
    case 'Processing':
      return 'text-blue-500 bg-blue-100 dark:bg-blue-900/50'
    case 'Pending':
      return 'text-yellow-500 bg-yellow-100 dark:bg-yellow-900/50'
    case 'Failed':
      return 'text-red-500 bg-red-100 dark:bg-red-900/50'
    default:
      return 'text-gray-500 bg-gray-100 dark:bg-gray-700'
  }
}

const getTypeIcon = (type: DocumentType) => {
  switch (type) {
    case 'Pdf':
      return DocumentTextIcon
    case 'Word':
      return DocumentIcon
    case 'Excel':
    case 'Csv':
      return TableCellsIcon
    case 'PowerPoint':
      return PresentationChartBarIcon
    case 'Text':
    case 'Markdown':
      return DocumentTextIcon
    case 'Html':
    case 'Json':
      return CodeBracketIcon
    case 'Image':
      return PhotoIcon
    default:
      return DocumentIcon
  }
}

const clearFilters = () => {
  searchQuery.value = ''
  selectedTypes.value = []
  selectedStatuses.value = []
}

const hasActiveFilters = computed(() => {
  return searchQuery.value || selectedTypes.value.length > 0 || selectedStatuses.value.length > 0
})
</script>

<template>
  <div class="space-y-4">
    <!-- Header with search and filters -->
    <div class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
      <!-- Search -->
      <div class="relative flex-1 max-w-md">
        <MagnifyingGlassIcon class="absolute left-3 top-1/2 h-5 w-5 -translate-y-1/2 text-gray-400" />
        <input
          v-model="searchQuery"
          type="text"
          class="input w-full pl-10"
          placeholder="Search documents..."
        />
      </div>

      <div class="flex items-center gap-2">
        <!-- Type Filter -->
        <Listbox v-model="selectedTypes" multiple>
          <div class="relative">
            <ListboxButton class="btn-ghost flex items-center gap-2">
              <FunnelIcon class="h-4 w-4" />
              Type
              <span v-if="selectedTypes.length > 0" class="rounded-full bg-aegis-100 px-2 text-xs text-aegis-700 dark:bg-aegis-900/50 dark:text-aegis-300">
                {{ selectedTypes.length }}
              </span>
            </ListboxButton>
            <ListboxOptions class="absolute right-0 z-10 mt-1 max-h-60 w-48 overflow-auto rounded-lg bg-white py-1 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-800">
              <ListboxOption
                v-for="type in documentTypes"
                :key="type.value"
                :value="type.value"
                v-slot="{ selected }"
                class="relative cursor-pointer select-none py-2 pl-10 pr-4 text-gray-900 hover:bg-gray-100 dark:text-gray-100 dark:hover:bg-gray-700"
              >
                <span :class="['block truncate', selected ? 'font-medium' : 'font-normal']">
                  {{ type.label }}
                </span>
                <span v-if="selected" class="absolute inset-y-0 left-0 flex items-center pl-3 text-aegis-600">
                  <CheckIcon class="h-4 w-4" />
                </span>
              </ListboxOption>
            </ListboxOptions>
          </div>
        </Listbox>

        <!-- Status Filter -->
        <Listbox v-model="selectedStatuses" multiple>
          <div class="relative">
            <ListboxButton class="btn-ghost flex items-center gap-2">
              <FunnelIcon class="h-4 w-4" />
              Status
              <span v-if="selectedStatuses.length > 0" class="rounded-full bg-aegis-100 px-2 text-xs text-aegis-700 dark:bg-aegis-900/50 dark:text-aegis-300">
                {{ selectedStatuses.length }}
              </span>
            </ListboxButton>
            <ListboxOptions class="absolute right-0 z-10 mt-1 max-h-60 w-48 overflow-auto rounded-lg bg-white py-1 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-800">
              <ListboxOption
                v-for="status in statusOptions"
                :key="status.value"
                :value="status.value"
                v-slot="{ selected }"
                class="relative cursor-pointer select-none py-2 pl-10 pr-4 text-gray-900 hover:bg-gray-100 dark:text-gray-100 dark:hover:bg-gray-700"
              >
                <span :class="['block truncate', selected ? 'font-medium' : 'font-normal']">
                  {{ status.label }}
                </span>
                <span v-if="selected" class="absolute inset-y-0 left-0 flex items-center pl-3 text-aegis-600">
                  <CheckIcon class="h-4 w-4" />
                </span>
              </ListboxOption>
            </ListboxOptions>
          </div>
        </Listbox>

        <!-- Sort -->
        <Listbox v-model="sortBy">
          <div class="relative">
            <ListboxButton class="btn-ghost flex items-center gap-2">
              <ArrowsUpDownIcon class="h-4 w-4" />
              {{ sortOptions.find(s => s.value === sortBy)?.label }}
            </ListboxButton>
            <ListboxOptions class="absolute right-0 z-10 mt-1 w-36 overflow-auto rounded-lg bg-white py-1 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-800">
              <ListboxOption
                v-for="option in sortOptions"
                :key="option.value"
                :value="option.value"
                v-slot="{ selected }"
                class="relative cursor-pointer select-none py-2 pl-10 pr-4 text-gray-900 hover:bg-gray-100 dark:text-gray-100 dark:hover:bg-gray-700"
              >
                <span :class="['block truncate', selected ? 'font-medium' : 'font-normal']">
                  {{ option.label }}
                </span>
                <span v-if="selected" class="absolute inset-y-0 left-0 flex items-center pl-3 text-aegis-600">
                  <CheckIcon class="h-4 w-4" />
                </span>
              </ListboxOption>
            </ListboxOptions>
          </div>
        </Listbox>

        <!-- Sort Order Toggle -->
        <button
          class="btn-ghost p-2"
          @click="sortOrder = sortOrder === 'asc' ? 'desc' : 'asc'"
          :title="sortOrder === 'asc' ? 'Ascending' : 'Descending'"
        >
          <ArrowsUpDownIcon
            class="h-4 w-4 transition-transform"
            :class="{ 'rotate-180': sortOrder === 'asc' }"
          />
        </button>

        <!-- Clear Filters -->
        <button
          v-if="hasActiveFilters"
          class="btn-ghost text-sm text-gray-500"
          @click="clearFilters"
        >
          Clear
        </button>

        <!-- Upload Button -->
        <button class="btn-primary" @click="emit('upload')">
          <DocumentArrowUpIcon class="h-4 w-4 mr-2" />
          Upload
        </button>
      </div>
    </div>

    <!-- Bulk Actions Bar -->
    <div
      v-if="selectedIds.size > 0"
      class="flex items-center justify-between rounded-lg bg-aegis-50 p-3 dark:bg-aegis-900/20"
    >
      <span class="text-sm font-medium text-aegis-700 dark:text-aegis-300">
        {{ selectedIds.size }} document{{ selectedIds.size !== 1 ? 's' : '' }} selected
      </span>
      <div class="flex items-center gap-2">
        <button
          class="btn-ghost text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-900/20"
          @click="handleBulkDelete"
        >
          <TrashIcon class="h-4 w-4 mr-1" />
          Delete
        </button>
        <button class="btn-ghost" @click="selectedIds = new Set(); selectAll = false">
          Cancel
        </button>
      </div>
    </div>

    <!-- Loading State -->
    <div v-if="loading" class="py-12 text-center">
      <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-aegis-600 mx-auto"></div>
      <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">Loading documents...</p>
    </div>

    <!-- Empty State -->
    <div v-else-if="documents.length === 0" class="py-12 text-center">
      <DocumentTextIcon class="mx-auto h-12 w-12 text-gray-400" />
      <h3 class="mt-4 text-lg font-medium text-gray-900 dark:text-white">No documents</h3>
      <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">
        Upload documents to get started with your knowledge base
      </p>
      <button class="btn-primary mt-4" @click="emit('upload')">
        <DocumentArrowUpIcon class="h-4 w-4 mr-2" />
        Upload Documents
      </button>
    </div>

    <!-- No Results -->
    <div v-else-if="filteredDocuments.length === 0" class="py-12 text-center">
      <MagnifyingGlassIcon class="mx-auto h-12 w-12 text-gray-400" />
      <h3 class="mt-4 text-lg font-medium text-gray-900 dark:text-white">No matching documents</h3>
      <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">
        Try adjusting your search or filters
      </p>
      <button class="btn-ghost mt-4" @click="clearFilters">
        Clear Filters
      </button>
    </div>

    <!-- Document List -->
    <div v-else class="space-y-2">
      <!-- Header Row -->
      <div class="flex items-center gap-4 px-4 py-2 text-xs font-medium text-gray-500 dark:text-gray-400">
        <div class="w-6">
          <input
            type="checkbox"
            :checked="selectAll"
            :indeterminate="selectedIds.size > 0 && selectedIds.size < filteredDocuments.length"
            class="rounded border-gray-300 text-aegis-600 focus:ring-aegis-500"
            @change="toggleSelectAll"
          />
        </div>
        <div class="flex-1">Name</div>
        <div class="w-20 text-center hidden sm:block">Type</div>
        <div class="w-24 text-center hidden md:block">Status</div>
        <div class="w-20 text-right hidden lg:block">Size</div>
        <div class="w-24 text-right hidden lg:block">Date</div>
        <div class="w-10"></div>
      </div>

      <!-- Document Rows -->
      <div
        v-for="doc in filteredDocuments"
        :key="doc.id"
        class="flex items-center gap-4 rounded-lg border border-gray-200 px-4 py-3 transition-colors hover:bg-gray-50 dark:border-gray-700 dark:hover:bg-gray-800/50"
        :class="{ 'bg-aegis-50 dark:bg-aegis-900/20 border-aegis-200 dark:border-aegis-800': isSelected(doc.id) }"
      >
        <!-- Checkbox -->
        <div class="w-6">
          <input
            type="checkbox"
            :checked="isSelected(doc.id)"
            class="rounded border-gray-300 text-aegis-600 focus:ring-aegis-500"
            @change="toggleSelection(doc.id)"
          />
        </div>

        <!-- Name -->
        <div class="flex-1 min-w-0 flex items-center gap-3">
          <div class="shrink-0 rounded-lg bg-gray-100 p-2 dark:bg-gray-800">
            <component :is="getTypeIcon(doc.type)" class="h-5 w-5 text-gray-500" />
          </div>
          <div class="min-w-0">
            <button
              class="text-left font-medium text-gray-900 dark:text-white hover:text-aegis-600 dark:hover:text-aegis-400 truncate block max-w-full"
              @click="emit('viewDetails', doc)"
            >
              {{ doc.name }}
            </button>
            <p class="text-xs text-gray-500 dark:text-gray-400 truncate">
              {{ doc.chunkCount }} chunks
            </p>
          </div>
        </div>

        <!-- Type Badge -->
        <div class="w-20 text-center hidden sm:block">
          <span class="inline-flex items-center rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-600 dark:bg-gray-800 dark:text-gray-400">
            {{ doc.type }}
          </span>
        </div>

        <!-- Status -->
        <div class="w-24 text-center hidden md:block">
          <span
            class="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs"
            :class="getStatusColor(doc.status)"
          >
            <component :is="getStatusIcon(doc.status)" class="h-3 w-3" :class="{ 'animate-spin': doc.status === 'Processing' }" />
            {{ doc.status }}
          </span>
        </div>

        <!-- Size -->
        <div class="w-20 text-right text-sm text-gray-500 dark:text-gray-400 hidden lg:block">
          {{ formatFileSize(doc.size) }}
        </div>

        <!-- Date -->
        <div class="w-24 text-right text-sm text-gray-500 dark:text-gray-400 hidden lg:block">
          {{ formatDate(doc.createdAt) }}
        </div>

        <!-- Actions Menu -->
        <div class="w-10">
          <Menu as="div" class="relative">
            <MenuButton class="btn-ghost p-1">
              <EllipsisVerticalIcon class="h-5 w-5 text-gray-400" />
            </MenuButton>
            <MenuItems class="absolute right-0 z-10 mt-1 w-48 origin-top-right rounded-lg bg-white py-1 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-800">
              <MenuItem v-slot="{ active }">
                <button
                  class="flex w-full items-center gap-2 px-4 py-2 text-sm"
                  :class="active ? 'bg-gray-100 text-gray-900 dark:bg-gray-700 dark:text-gray-100' : 'text-gray-700 dark:text-gray-300'"
                  @click="emit('viewDetails', doc)"
                >
                  <DocumentTextIcon class="h-4 w-4" />
                  View Details
                </button>
              </MenuItem>
              <MenuItem v-slot="{ active }">
                <button
                  class="flex w-full items-center gap-2 px-4 py-2 text-sm"
                  :class="active ? 'bg-gray-100 text-gray-900 dark:bg-gray-700 dark:text-gray-100' : 'text-gray-700 dark:text-gray-300'"
                  @click="emit('download', doc.id)"
                >
                  <ArrowDownTrayIcon class="h-4 w-4" />
                  Download
                </button>
              </MenuItem>
              <MenuItem v-slot="{ active }">
                <button
                  class="flex w-full items-center gap-2 px-4 py-2 text-sm"
                  :class="active ? 'bg-gray-100 text-gray-900 dark:bg-gray-700 dark:text-gray-100' : 'text-gray-700 dark:text-gray-300'"
                  @click="emit('reindex', doc.id)"
                >
                  <ArrowPathIcon class="h-4 w-4" />
                  Reindex
                </button>
              </MenuItem>
              <MenuItem v-slot="{ active }">
                <button
                  class="flex w-full items-center gap-2 px-4 py-2 text-sm text-red-600 dark:text-red-400"
                  :class="active ? 'bg-red-50 dark:bg-red-900/20' : ''"
                  @click="emit('delete', doc.id)"
                >
                  <TrashIcon class="h-4 w-4" />
                  Delete
                </button>
              </MenuItem>
            </MenuItems>
          </Menu>
        </div>
      </div>
    </div>

    <!-- Results Count -->
    <div v-if="filteredDocuments.length > 0" class="text-sm text-gray-500 dark:text-gray-400">
      Showing {{ filteredDocuments.length }} of {{ documents.length }} document{{ documents.length !== 1 ? 's' : '' }}
    </div>
  </div>
</template>
