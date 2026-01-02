import { ref, computed, watch } from 'vue'
import type { BulkAction } from '@/types/upload'

interface UseBulkSelectionOptions<T> {
  items: () => T[]
  getId: (item: T) => string
  onAction?: (action: BulkAction, selectedIds: string[]) => Promise<void>
}

export function useBulkSelection<T>(options: UseBulkSelectionOptions<T>) {
  const { items, getId, onAction } = options

  const selectedIds = ref<Set<string>>(new Set())
  const isSelectionMode = ref(false)
  const isProcessing = ref(false)

  // Computed
  const selectedCount = computed(() => selectedIds.value.size)

  const allSelected = computed(() => {
    const currentItems = items()
    return currentItems.length > 0 && currentItems.every(item => selectedIds.value.has(getId(item)))
  })

  const someSelected = computed(() => {
    const currentItems = items()
    return currentItems.some(item => selectedIds.value.has(getId(item))) && !allSelected.value
  })

  const selectedItems = computed(() => {
    const currentItems = items()
    return currentItems.filter(item => selectedIds.value.has(getId(item)))
  })

  // Watch items to clean up stale selections
  watch(() => items(), (currentItems) => {
    const currentIds = new Set(currentItems.map(getId))
    const newSelectedIds = new Set<string>()

    selectedIds.value.forEach(id => {
      if (currentIds.has(id)) {
        newSelectedIds.add(id)
      }
    })

    selectedIds.value = newSelectedIds
  }, { deep: true })

  // Toggle selection mode
  function toggleSelectionMode(): void {
    isSelectionMode.value = !isSelectionMode.value
    if (!isSelectionMode.value) {
      clearSelection()
    }
  }

  // Enable selection mode
  function enableSelectionMode(): void {
    isSelectionMode.value = true
  }

  // Disable selection mode
  function disableSelectionMode(): void {
    isSelectionMode.value = false
    clearSelection()
  }

  // Toggle individual item selection
  function toggleItem(id: string): void {
    if (selectedIds.value.has(id)) {
      selectedIds.value.delete(id)
    } else {
      selectedIds.value.add(id)
    }
    // Force reactivity
    selectedIds.value = new Set(selectedIds.value)
  }

  // Select specific item
  function selectItem(id: string): void {
    selectedIds.value.add(id)
    selectedIds.value = new Set(selectedIds.value)
  }

  // Deselect specific item
  function deselectItem(id: string): void {
    selectedIds.value.delete(id)
    selectedIds.value = new Set(selectedIds.value)
  }

  // Check if item is selected
  function isSelected(id: string): boolean {
    return selectedIds.value.has(id)
  }

  // Select all items
  function selectAll(): void {
    const currentItems = items()
    currentItems.forEach(item => selectedIds.value.add(getId(item)))
    selectedIds.value = new Set(selectedIds.value)
  }

  // Clear all selections
  function clearSelection(): void {
    selectedIds.value = new Set()
  }

  // Toggle all (select all if not all selected, otherwise clear)
  function toggleAll(): void {
    if (allSelected.value) {
      clearSelection()
    } else {
      selectAll()
    }
  }

  // Execute bulk action
  async function executeAction(action: BulkAction): Promise<boolean> {
    if (selectedCount.value === 0) return false

    if (action.requiresConfirmation) {
      const message = action.confirmationMessage ||
        `Are you sure you want to ${action.label.toLowerCase()} ${selectedCount.value} item(s)?`

      if (!confirm(message)) {
        return false
      }
    }

    isProcessing.value = true

    try {
      if (onAction) {
        await onAction(action, Array.from(selectedIds.value))
      }
      clearSelection()
      return true
    } catch (error) {
      console.error('Bulk action failed:', error)
      return false
    } finally {
      isProcessing.value = false
    }
  }

  // Get selected IDs as array
  function getSelectedIds(): string[] {
    return Array.from(selectedIds.value)
  }

  return {
    // State
    selectedIds,
    isSelectionMode,
    isProcessing,

    // Computed
    selectedCount,
    allSelected,
    someSelected,
    selectedItems,

    // Actions
    toggleSelectionMode,
    enableSelectionMode,
    disableSelectionMode,
    toggleItem,
    selectItem,
    deselectItem,
    isSelected,
    selectAll,
    clearSelection,
    toggleAll,
    executeAction,
    getSelectedIds
  }
}
