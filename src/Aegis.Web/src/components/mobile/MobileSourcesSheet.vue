<script setup lang="ts">
import { ref, watch } from 'vue'
import { XMarkIcon, DocumentTextIcon } from '@heroicons/vue/24/outline'
import type { SourceReference } from '@/types'

interface Props {
  sources: SourceReference[]
  open?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  open: false
})

const emit = defineEmits<{
  close: []
  'view-source': [source: SourceReference]
}>()

const isVisible = ref(false)
const sheetRef = ref<HTMLElement>()

// Animation control
watch(() => props.open, (open) => {
  if (open) {
    isVisible.value = true
    // Prevent body scroll
    document.body.style.overflow = 'hidden'
  } else {
    document.body.style.overflow = ''
  }
})

function handleTransitionEnd() {
  if (!props.open) {
    isVisible.value = false
  }
}

function handleBackdropClick() {
  emit('close')
}

// Drag to dismiss
let startY = 0
let currentY = 0
let isDragging = false

function handleTouchStart(e: TouchEvent) {
  if (!sheetRef.value) return
  startY = e.touches[0].clientY
  isDragging = true
}

function handleTouchMove(e: TouchEvent) {
  if (!isDragging || !sheetRef.value) return

  currentY = e.touches[0].clientY
  const diff = currentY - startY

  // Only allow dragging down
  if (diff > 0) {
    sheetRef.value.style.transform = `translateY(${diff}px)`
  }
}

function handleTouchEnd() {
  if (!isDragging || !sheetRef.value) return

  const diff = currentY - startY
  isDragging = false

  // If dragged more than 100px, close
  if (diff > 100) {
    emit('close')
  }

  // Reset transform
  sheetRef.value.style.transform = ''
}
</script>

<template>
  <Teleport to="body">
    <div
      v-if="isVisible"
      class="fixed inset-0 z-50 md:hidden"
      @transitionend="handleTransitionEnd"
    >
      <!-- Backdrop -->
      <div
        class="absolute inset-0 bg-black transition-opacity duration-300"
        :class="open ? 'opacity-50' : 'opacity-0'"
        @click="handleBackdropClick"
      />

      <!-- Sheet -->
      <div
        ref="sheetRef"
        class="absolute bottom-0 left-0 right-0 max-h-[80vh] transform rounded-t-2xl bg-white shadow-xl transition-transform duration-300 ease-out dark:bg-gray-800"
        :class="open ? 'translate-y-0' : 'translate-y-full'"
        @touchstart="handleTouchStart"
        @touchmove="handleTouchMove"
        @touchend="handleTouchEnd"
      >
        <!-- Handle -->
        <div class="flex justify-center py-3">
          <div class="h-1 w-10 rounded-full bg-gray-300 dark:bg-gray-600" />
        </div>

        <!-- Header -->
        <div class="flex items-center justify-between border-b border-gray-200 px-4 pb-3 dark:border-gray-700">
          <h2 class="text-lg font-semibold text-gray-900 dark:text-white">
            Sources ({{ sources.length }})
          </h2>
          <button
            class="flex h-8 w-8 items-center justify-center rounded-full text-gray-500 hover:bg-gray-100 dark:hover:bg-gray-700"
            @click="emit('close')"
          >
            <XMarkIcon class="h-5 w-5" />
          </button>
        </div>

        <!-- Sources list -->
        <div class="overflow-y-auto p-4" style="max-height: calc(80vh - 100px)">
          <div class="space-y-3">
            <button
              v-for="(source, idx) in sources"
              :key="idx"
              class="w-full rounded-xl border border-gray-200 bg-gray-50 p-4 text-left transition-colors active:bg-gray-100 dark:border-gray-700 dark:bg-gray-700/50 dark:active:bg-gray-700"
              @click="emit('view-source', source)"
            >
              <div class="flex items-start gap-3">
                <div class="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-white shadow-sm dark:bg-gray-600">
                  <DocumentTextIcon class="h-5 w-5 text-gray-500 dark:text-gray-400" />
                </div>
                <div class="flex-1 min-w-0">
                  <div class="flex items-center justify-between gap-2">
                    <p class="truncate font-medium text-gray-900 dark:text-white">
                      {{ source.documentName }}
                    </p>
                    <span
                      class="shrink-0 rounded-full px-2 py-0.5 text-xs font-medium"
                      :class="source.relevanceScore >= 0.8
                        ? 'bg-green-100 text-green-700 dark:bg-green-900/50 dark:text-green-400'
                        : source.relevanceScore >= 0.5
                          ? 'bg-yellow-100 text-yellow-700 dark:bg-yellow-900/50 dark:text-yellow-400'
                          : 'bg-gray-100 text-gray-600 dark:bg-gray-600 dark:text-gray-300'"
                    >
                      {{ Math.round(source.relevanceScore * 100) }}%
                    </span>
                  </div>
                  <p
                    v-if="source.excerpt"
                    class="mt-1 line-clamp-2 text-sm text-gray-600 dark:text-gray-400"
                  >
                    {{ source.excerpt }}
                  </p>
                </div>
              </div>
            </button>
          </div>
        </div>
      </div>
    </div>
  </Teleport>
</template>
