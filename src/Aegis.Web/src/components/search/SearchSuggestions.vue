<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import { useSearchStore } from '@/stores/search'
import {
  MagnifyingGlassIcon,
  ClockIcon,
  BookmarkIcon,
  DocumentTextIcon,
  FolderIcon
} from '@heroicons/vue/24/outline'
import type { SearchSuggestion } from '@/types/search'

interface Props {
  query: string
  show: boolean
}

const props = defineProps<Props>()

const emit = defineEmits<{
  select: [suggestion: SearchSuggestion]
  close: []
}>()

const searchStore = useSearchStore()
const selectedIndex = ref(-1)
const containerRef = ref<HTMLDivElement | null>(null)

const suggestions = computed(() => searchStore.suggestions)
const hasSuggestions = computed(() => suggestions.value.length > 0)

// Watch for query changes and generate suggestions
watch(() => props.query, (newQuery) => {
  if (newQuery) {
    searchStore.generateSuggestions(newQuery)
    selectedIndex.value = -1
  } else {
    searchStore.clearSuggestions()
  }
}, { immediate: true })

// Get icon for suggestion type
function getIcon(type: SearchSuggestion['type']) {
  switch (type) {
    case 'query':
      return MagnifyingGlassIcon
    case 'recent':
      return ClockIcon
    case 'saved':
      return BookmarkIcon
    case 'document':
      return DocumentTextIcon
    case 'workspace':
      return FolderIcon
    default:
      return MagnifyingGlassIcon
  }
}

// Get label for suggestion type
function getTypeLabel(type: SearchSuggestion['type']): string {
  switch (type) {
    case 'query':
      return 'Search'
    case 'recent':
      return 'Recent'
    case 'saved':
      return 'Saved'
    case 'document':
      return 'Document'
    case 'workspace':
      return 'Workspace'
    default:
      return ''
  }
}

// Handle keyboard navigation
function handleKeyDown(event: KeyboardEvent) {
  if (!props.show || !hasSuggestions.value) return

  switch (event.key) {
    case 'ArrowDown':
      event.preventDefault()
      selectedIndex.value = Math.min(selectedIndex.value + 1, suggestions.value.length - 1)
      scrollToSelected()
      break
    case 'ArrowUp':
      event.preventDefault()
      selectedIndex.value = Math.max(selectedIndex.value - 1, -1)
      scrollToSelected()
      break
    case 'Enter':
      if (selectedIndex.value >= 0) {
        event.preventDefault()
        selectSuggestion(suggestions.value[selectedIndex.value])
      }
      break
    case 'Escape':
      emit('close')
      break
  }
}

function scrollToSelected() {
  if (containerRef.value && selectedIndex.value >= 0) {
    const items = containerRef.value.querySelectorAll('[data-suggestion]')
    if (items[selectedIndex.value]) {
      items[selectedIndex.value].scrollIntoView({ block: 'nearest' })
    }
  }
}

function selectSuggestion(suggestion: SearchSuggestion) {
  emit('select', suggestion)
  searchStore.hideSuggestions()
}

function handleMouseEnter(index: number) {
  selectedIndex.value = index
}

// Attach keyboard listener
onMounted(() => {
  document.addEventListener('keydown', handleKeyDown)
})

onUnmounted(() => {
  document.removeEventListener('keydown', handleKeyDown)
})
</script>

<template>
  <Transition
    enter-active-class="transition ease-out duration-200"
    enter-from-class="opacity-0 translate-y-1"
    enter-to-class="opacity-100 translate-y-0"
    leave-active-class="transition ease-in duration-150"
    leave-from-class="opacity-100 translate-y-0"
    leave-to-class="opacity-0 translate-y-1"
  >
    <div
      v-if="show && hasSuggestions"
      ref="containerRef"
      class="absolute top-full left-0 right-0 mt-1 bg-white dark:bg-gray-800 rounded-xl shadow-lg border border-gray-200 dark:border-gray-700 overflow-hidden z-50 max-h-80 overflow-y-auto"
    >
      <ul role="listbox" class="py-1">
        <li
          v-for="(suggestion, index) in suggestions"
          :key="`${suggestion.type}-${index}`"
          data-suggestion
          role="option"
          :aria-selected="selectedIndex === index"
          class="px-3 py-2 flex items-center gap-3 cursor-pointer transition-colors"
          :class="[
            selectedIndex === index
              ? 'bg-aegis-50 dark:bg-aegis-900/50'
              : 'hover:bg-gray-50 dark:hover:bg-gray-700/50'
          ]"
          @click="selectSuggestion(suggestion)"
          @mouseenter="handleMouseEnter(index)"
        >
          <!-- Icon -->
          <component
            :is="getIcon(suggestion.type)"
            class="h-4 w-4 flex-shrink-0"
            :class="[
              suggestion.type === 'saved'
                ? 'text-aegis-500'
                : suggestion.type === 'recent'
                  ? 'text-gray-400'
                  : 'text-gray-500'
            ]"
          />

          <!-- Text -->
          <div class="flex-1 min-w-0">
            <p class="text-sm text-gray-900 dark:text-gray-100 truncate">
              {{ suggestion.text }}
            </p>
          </div>

          <!-- Type badge and metadata -->
          <div class="flex items-center gap-2 flex-shrink-0">
            <span
              v-if="suggestion.metadata?.count !== undefined"
              class="text-xs text-gray-400"
            >
              {{ suggestion.metadata.count }} results
            </span>
            <span
              class="text-xs px-2 py-0.5 rounded-full"
              :class="{
                'bg-aegis-100 text-aegis-700 dark:bg-aegis-900/50 dark:text-aegis-300': suggestion.type === 'saved',
                'bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-400': suggestion.type === 'recent',
                'bg-blue-100 text-blue-700 dark:bg-blue-900/50 dark:text-blue-300': suggestion.type === 'query',
                'bg-green-100 text-green-700 dark:bg-green-900/50 dark:text-green-300': suggestion.type === 'document',
                'bg-purple-100 text-purple-700 dark:bg-purple-900/50 dark:text-purple-300': suggestion.type === 'workspace'
              }"
            >
              {{ getTypeLabel(suggestion.type) }}
            </span>
          </div>
        </li>
      </ul>

      <!-- Keyboard hint -->
      <div class="px-3 py-2 border-t border-gray-100 dark:border-gray-700 text-xs text-gray-400 dark:text-gray-500 flex items-center justify-between">
        <span>
          <kbd class="px-1 py-0.5 bg-gray-100 dark:bg-gray-700 rounded text-[10px]">↑</kbd>
          <kbd class="px-1 py-0.5 bg-gray-100 dark:bg-gray-700 rounded text-[10px] ml-0.5">↓</kbd>
          <span class="ml-1">to navigate</span>
        </span>
        <span>
          <kbd class="px-1 py-0.5 bg-gray-100 dark:bg-gray-700 rounded text-[10px]">Enter</kbd>
          <span class="ml-1">to select</span>
        </span>
        <span>
          <kbd class="px-1 py-0.5 bg-gray-100 dark:bg-gray-700 rounded text-[10px]">Esc</kbd>
          <span class="ml-1">to close</span>
        </span>
      </div>
    </div>
  </Transition>
</template>
