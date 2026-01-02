<script setup lang="ts">
import { computed } from 'vue'
import {
  FolderIcon,
  DocumentTextIcon,
  MagnifyingGlassIcon,
  ChevronRightIcon
} from '@heroicons/vue/24/outline'
import type { Workspace } from '@/types'

const props = defineProps<{
  workspace: Workspace
}>()

const emit = defineEmits<{
  click: [workspace: Workspace]
}>()

const formattedDate = computed(() => {
  const date = new Date(props.workspace.createdAt)
  return date.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric'
  })
})

const documentCount = computed(() => props.workspace.stats?.documentCount || 0)
const queryCount = computed(() => props.workspace.stats?.queryCount || 0)
</script>

<template>
  <button
    class="w-full flex items-center gap-3 p-4 bg-white dark:bg-gray-800 border-b border-gray-100 dark:border-gray-700 active:bg-gray-50 dark:active:bg-gray-700/50 text-left"
    @click="emit('click', workspace)"
  >
    <!-- Icon -->
    <div class="shrink-0">
      <div class="flex h-12 w-12 items-center justify-center rounded-xl bg-aegis-100 dark:bg-aegis-900/50">
        <FolderIcon class="h-6 w-6 text-aegis-600 dark:text-aegis-400" />
      </div>
    </div>

    <!-- Content -->
    <div class="flex-1 min-w-0">
      <h3 class="text-base font-semibold text-gray-900 dark:text-white truncate">
        {{ workspace.name }}
      </h3>
      <p v-if="workspace.description" class="text-sm text-gray-500 dark:text-gray-400 truncate">
        {{ workspace.description }}
      </p>
      <div class="mt-1 flex items-center gap-3 text-xs text-gray-400">
        <span class="flex items-center gap-1">
          <DocumentTextIcon class="h-3.5 w-3.5" />
          {{ documentCount }}
        </span>
        <span class="flex items-center gap-1">
          <MagnifyingGlassIcon class="h-3.5 w-3.5" />
          {{ queryCount }}
        </span>
        <span>{{ formattedDate }}</span>
      </div>
    </div>

    <!-- Arrow -->
    <ChevronRightIcon class="h-5 w-5 shrink-0 text-gray-400" />
  </button>
</template>
