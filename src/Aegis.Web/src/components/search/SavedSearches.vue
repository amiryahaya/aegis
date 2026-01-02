<script setup lang="ts">
import { ref, computed } from 'vue'
import { useSearchStore } from '@/stores/search'
import {
  BookmarkIcon,
  PlusIcon,
  TrashIcon,
  PencilIcon,
  StarIcon,
  MagnifyingGlassIcon
} from '@heroicons/vue/24/outline'
import { StarIcon as StarIconSolid } from '@heroicons/vue/24/solid'
import { Dialog, DialogPanel, DialogTitle, TransitionChild, TransitionRoot } from '@headlessui/vue'
import type { SavedSearch, SavedSearchColor } from '@/types/search'

const searchStore = useSearchStore()

const showSaveDialog = ref(false)
const showEditDialog = ref(false)
const editingSearch = ref<SavedSearch | null>(null)

// Form state
const formName = ref('')
const formColor = ref<SavedSearchColor>('blue')

const colorOptions: { value: SavedSearchColor; label: string; class: string }[] = [
  { value: 'blue', label: 'Blue', class: 'bg-blue-500' },
  { value: 'green', label: 'Green', class: 'bg-green-500' },
  { value: 'purple', label: 'Purple', class: 'bg-purple-500' },
  { value: 'orange', label: 'Orange', class: 'bg-orange-500' },
  { value: 'pink', label: 'Pink', class: 'bg-pink-500' },
  { value: 'teal', label: 'Teal', class: 'bg-teal-500' },
  { value: 'red', label: 'Red', class: 'bg-red-500' },
  { value: 'yellow', label: 'Yellow', class: 'bg-yellow-500' }
]

const hasSavedSearches = computed(() => searchStore.savedSearches.length > 0)

function getColorClass(color?: SavedSearchColor): string {
  const colorMap: Record<SavedSearchColor, string> = {
    blue: 'bg-blue-100 text-blue-700 dark:bg-blue-900/50 dark:text-blue-300',
    green: 'bg-green-100 text-green-700 dark:bg-green-900/50 dark:text-green-300',
    purple: 'bg-purple-100 text-purple-700 dark:bg-purple-900/50 dark:text-purple-300',
    orange: 'bg-orange-100 text-orange-700 dark:bg-orange-900/50 dark:text-orange-300',
    pink: 'bg-pink-100 text-pink-700 dark:bg-pink-900/50 dark:text-pink-300',
    teal: 'bg-teal-100 text-teal-700 dark:bg-teal-900/50 dark:text-teal-300',
    red: 'bg-red-100 text-red-700 dark:bg-red-900/50 dark:text-red-300',
    yellow: 'bg-yellow-100 text-yellow-700 dark:bg-yellow-900/50 dark:text-yellow-300'
  }
  return colorMap[color || 'blue']
}

function openSaveDialog() {
  formName.value = ''
  formColor.value = 'blue'
  showSaveDialog.value = true
}

function closeSaveDialog() {
  showSaveDialog.value = false
  formName.value = ''
}

function saveCurrentSearch() {
  if (!formName.value.trim() || !searchStore.currentQuery) return

  searchStore.createSavedSearch({
    name: formName.value.trim(),
    query: searchStore.currentQuery,
    filters: { ...searchStore.filters },
    color: formColor.value
  })

  closeSaveDialog()
}

function openEditDialog(search: SavedSearch) {
  editingSearch.value = search
  formName.value = search.name
  formColor.value = search.color || 'blue'
  showEditDialog.value = true
}

function closeEditDialog() {
  showEditDialog.value = false
  editingSearch.value = null
  formName.value = ''
}

function updateSearch() {
  if (!editingSearch.value || !formName.value.trim()) return

  searchStore.updateSavedSearch(editingSearch.value.id, {
    name: formName.value.trim(),
    color: formColor.value
  })

  closeEditDialog()
}

function deleteSearch(id: string) {
  if (confirm('Are you sure you want to delete this saved search?')) {
    searchStore.deleteSavedSearch(id)
  }
}

function executeSearch(id: string) {
  searchStore.executeSavedSearch(id)
}

function toggleDefault(search: SavedSearch) {
  if (search.isDefault) {
    searchStore.updateSavedSearch(search.id, { isDefault: false })
  } else {
    searchStore.setDefaultSavedSearch(search.id)
  }
}

function formatDate(dateString: string): string {
  return new Date(dateString).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric'
  })
}
</script>

<template>
  <div class="space-y-4">
    <!-- Header -->
    <div class="flex items-center justify-between">
      <h3 class="text-sm font-medium text-gray-900 dark:text-gray-100 flex items-center gap-2">
        <BookmarkIcon class="h-4 w-4" />
        Saved Searches
      </h3>
      <button
        v-if="searchStore.currentQuery"
        @click="openSaveDialog"
        class="text-sm text-aegis-600 dark:text-aegis-400 hover:text-aegis-700 dark:hover:text-aegis-300 flex items-center gap-1"
      >
        <PlusIcon class="h-4 w-4" />
        Save Current
      </button>
    </div>

    <!-- Empty State -->
    <div
      v-if="!hasSavedSearches"
      class="text-center py-6 text-gray-500 dark:text-gray-400"
    >
      <BookmarkIcon class="h-8 w-8 mx-auto mb-2 opacity-50" />
      <p class="text-sm">No saved searches yet</p>
      <p class="text-xs mt-1">Save your frequently used searches for quick access</p>
    </div>

    <!-- Saved Searches List -->
    <div v-else class="space-y-2">
      <div
        v-for="search in searchStore.savedSearches"
        :key="search.id"
        class="group flex items-center gap-3 p-3 rounded-lg bg-gray-50 dark:bg-gray-800/50 hover:bg-gray-100 dark:hover:bg-gray-700/50 transition-colors cursor-pointer"
        @click="executeSearch(search.id)"
      >
        <!-- Color indicator -->
        <div
          class="w-2 h-8 rounded-full flex-shrink-0"
          :class="getColorClass(search.color).replace('text-', 'bg-').split(' ')[0]"
        />

        <!-- Search info -->
        <div class="flex-1 min-w-0">
          <div class="flex items-center gap-2">
            <span class="font-medium text-gray-900 dark:text-gray-100 truncate">
              {{ search.name }}
            </span>
            <button
              @click.stop="toggleDefault(search)"
              class="opacity-0 group-hover:opacity-100 transition-opacity"
              :title="search.isDefault ? 'Remove as default' : 'Set as default'"
            >
              <StarIconSolid v-if="search.isDefault" class="h-4 w-4 text-yellow-500" />
              <StarIcon v-else class="h-4 w-4 text-gray-400 hover:text-yellow-500" />
            </button>
          </div>
          <div class="flex items-center gap-2 text-xs text-gray-500 dark:text-gray-400">
            <MagnifyingGlassIcon class="h-3 w-3" />
            <span class="truncate">{{ search.query }}</span>
          </div>
          <div class="text-xs text-gray-400 dark:text-gray-500 mt-0.5">
            Used {{ search.useCount }} times
            <span v-if="search.lastUsedAt">
              · Last used {{ formatDate(search.lastUsedAt) }}
            </span>
          </div>
        </div>

        <!-- Actions -->
        <div class="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
          <button
            @click.stop="openEditDialog(search)"
            class="p-1.5 rounded-md hover:bg-gray-200 dark:hover:bg-gray-600 text-gray-500 dark:text-gray-400"
            title="Edit"
          >
            <PencilIcon class="h-4 w-4" />
          </button>
          <button
            @click.stop="deleteSearch(search.id)"
            class="p-1.5 rounded-md hover:bg-red-100 dark:hover:bg-red-900/30 text-red-500"
            title="Delete"
          >
            <TrashIcon class="h-4 w-4" />
          </button>
        </div>
      </div>
    </div>

    <!-- Save Search Dialog -->
    <TransitionRoot appear :show="showSaveDialog" as="template">
      <Dialog as="div" class="relative z-50" @close="closeSaveDialog">
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
              <DialogPanel class="w-full max-w-md transform overflow-hidden rounded-2xl bg-white dark:bg-gray-800 p-6 shadow-xl transition-all">
                <DialogTitle class="text-lg font-semibold text-gray-900 dark:text-gray-100">
                  Save Search
                </DialogTitle>

                <div class="mt-4 space-y-4">
                  <!-- Current query display -->
                  <div class="p-3 bg-gray-100 dark:bg-gray-700 rounded-lg">
                    <p class="text-xs text-gray-500 dark:text-gray-400 mb-1">Search query</p>
                    <p class="text-sm text-gray-900 dark:text-gray-100 font-medium">
                      {{ searchStore.currentQuery }}
                    </p>
                  </div>

                  <!-- Name input -->
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                      Name
                    </label>
                    <input
                      v-model="formName"
                      type="text"
                      class="input w-full"
                      placeholder="Enter a name for this search"
                    />
                  </div>

                  <!-- Color picker -->
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                      Color
                    </label>
                    <div class="flex gap-2">
                      <button
                        v-for="color in colorOptions"
                        :key="color.value"
                        @click="formColor = color.value"
                        class="w-8 h-8 rounded-full transition-transform"
                        :class="[
                          color.class,
                          formColor === color.value ? 'ring-2 ring-offset-2 ring-gray-900 dark:ring-gray-100 scale-110' : 'hover:scale-110'
                        ]"
                        :title="color.label"
                      />
                    </div>
                  </div>
                </div>

                <div class="mt-6 flex justify-end gap-3">
                  <button @click="closeSaveDialog" class="btn-secondary">
                    Cancel
                  </button>
                  <button
                    @click="saveCurrentSearch"
                    :disabled="!formName.trim()"
                    class="btn-primary"
                  >
                    Save Search
                  </button>
                </div>
              </DialogPanel>
            </TransitionChild>
          </div>
        </div>
      </Dialog>
    </TransitionRoot>

    <!-- Edit Search Dialog -->
    <TransitionRoot appear :show="showEditDialog" as="template">
      <Dialog as="div" class="relative z-50" @close="closeEditDialog">
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
              <DialogPanel class="w-full max-w-md transform overflow-hidden rounded-2xl bg-white dark:bg-gray-800 p-6 shadow-xl transition-all">
                <DialogTitle class="text-lg font-semibold text-gray-900 dark:text-gray-100">
                  Edit Saved Search
                </DialogTitle>

                <div class="mt-4 space-y-4">
                  <!-- Name input -->
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                      Name
                    </label>
                    <input
                      v-model="formName"
                      type="text"
                      class="input w-full"
                      placeholder="Enter a name for this search"
                    />
                  </div>

                  <!-- Color picker -->
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                      Color
                    </label>
                    <div class="flex gap-2">
                      <button
                        v-for="color in colorOptions"
                        :key="color.value"
                        @click="formColor = color.value"
                        class="w-8 h-8 rounded-full transition-transform"
                        :class="[
                          color.class,
                          formColor === color.value ? 'ring-2 ring-offset-2 ring-gray-900 dark:ring-gray-100 scale-110' : 'hover:scale-110'
                        ]"
                        :title="color.label"
                      />
                    </div>
                  </div>
                </div>

                <div class="mt-6 flex justify-end gap-3">
                  <button @click="closeEditDialog" class="btn-secondary">
                    Cancel
                  </button>
                  <button
                    @click="updateSearch"
                    :disabled="!formName.trim()"
                    class="btn-primary"
                  >
                    Save Changes
                  </button>
                </div>
              </DialogPanel>
            </TransitionChild>
          </div>
        </div>
      </Dialog>
    </TransitionRoot>
  </div>
</template>
