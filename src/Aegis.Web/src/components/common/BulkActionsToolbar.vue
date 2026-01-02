<script setup lang="ts">
import { computed } from 'vue'
import {
  XMarkIcon,
  TrashIcon,
  ArrowDownTrayIcon,
  ArchiveBoxIcon,
  FolderArrowDownIcon,
  ShareIcon,
  TagIcon
} from '@heroicons/vue/24/outline'
import type { BulkAction } from '@/types/upload'

interface Props {
  selectedCount: number
  allSelected: boolean
  someSelected: boolean
  actions?: BulkAction[]
  isProcessing?: boolean
  itemLabel?: string
}

const props = withDefaults(defineProps<Props>(), {
  isProcessing: false,
  itemLabel: 'item'
})

const emit = defineEmits<{
  toggleAll: []
  clearSelection: []
  action: [action: BulkAction]
}>()

const defaultActions: BulkAction[] = [
  {
    id: 'export',
    label: 'Export',
    icon: 'ArrowDownTrayIcon',
    variant: 'default'
  },
  {
    id: 'archive',
    label: 'Archive',
    icon: 'ArchiveBoxIcon',
    variant: 'warning',
    requiresConfirmation: true
  },
  {
    id: 'delete',
    label: 'Delete',
    icon: 'TrashIcon',
    variant: 'danger',
    requiresConfirmation: true,
    confirmationMessage: 'Are you sure you want to delete the selected items? This action cannot be undone.'
  }
]

const availableActions = computed(() => props.actions || defaultActions)

const selectionLabel = computed(() => {
  const plural = props.selectedCount !== 1 ? 's' : ''
  return `${props.selectedCount} ${props.itemLabel}${plural} selected`
})

function getActionIcon(iconName?: string) {
  switch (iconName) {
    case 'TrashIcon': return TrashIcon
    case 'ArrowDownTrayIcon': return ArrowDownTrayIcon
    case 'ArchiveBoxIcon': return ArchiveBoxIcon
    case 'FolderArrowDownIcon': return FolderArrowDownIcon
    case 'ShareIcon': return ShareIcon
    case 'TagIcon': return TagIcon
    default: return null
  }
}

function getActionButtonClass(variant?: BulkAction['variant']): string {
  switch (variant) {
    case 'danger':
      return 'text-red-600 hover:bg-red-100 dark:text-red-400 dark:hover:bg-red-900/30'
    case 'warning':
      return 'text-yellow-600 hover:bg-yellow-100 dark:text-yellow-400 dark:hover:bg-yellow-900/30'
    default:
      return 'text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700'
  }
}
</script>

<template>
  <Transition
    enter-active-class="transition ease-out duration-200"
    enter-from-class="opacity-0 -translate-y-2"
    enter-to-class="opacity-100 translate-y-0"
    leave-active-class="transition ease-in duration-150"
    leave-from-class="opacity-100 translate-y-0"
    leave-to-class="opacity-0 -translate-y-2"
  >
    <div
      v-if="selectedCount > 0"
      class="sticky top-0 z-20 bg-aegis-50 dark:bg-aegis-900/50 border border-aegis-200 dark:border-aegis-700 rounded-lg p-3 mb-4 flex items-center justify-between gap-4"
    >
      <!-- Left: Selection info -->
      <div class="flex items-center gap-4">
        <!-- Select all checkbox -->
        <label class="flex items-center gap-2 cursor-pointer">
          <input
            type="checkbox"
            :checked="allSelected"
            :indeterminate="someSelected"
            @change="emit('toggleAll')"
            class="h-4 w-4 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500"
          />
          <span class="text-sm text-gray-700 dark:text-gray-300">
            {{ allSelected ? 'Deselect all' : 'Select all' }}
          </span>
        </label>

        <span class="text-sm font-medium text-aegis-700 dark:text-aegis-300">
          {{ selectionLabel }}
        </span>
      </div>

      <!-- Right: Actions -->
      <div class="flex items-center gap-2">
        <!-- Action buttons -->
        <button
          v-for="action in availableActions"
          :key="action.id"
          @click="emit('action', action)"
          :disabled="isProcessing"
          class="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-md transition-colors disabled:opacity-50"
          :class="getActionButtonClass(action.variant)"
          :title="action.label"
        >
          <component
            v-if="getActionIcon(action.icon)"
            :is="getActionIcon(action.icon)"
            class="h-4 w-4"
          />
          <span class="hidden sm:inline">{{ action.label }}</span>
        </button>

        <!-- Clear selection -->
        <button
          @click="emit('clearSelection')"
          class="p-1.5 text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-md transition-colors"
          title="Clear selection"
        >
          <XMarkIcon class="h-5 w-5" />
        </button>
      </div>
    </div>
  </Transition>
</template>
