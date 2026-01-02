<script setup lang="ts">
import { ref, computed } from 'vue'
import { CloudArrowUpIcon } from '@heroicons/vue/24/outline'

interface Props {
  accept?: string
  multiple?: boolean
  maxSize?: number // in MB
  maxFiles?: number
  disabled?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  accept: '*/*',
  multiple: true,
  maxSize: 50, // 50MB default
  maxFiles: 10,
  disabled: false
})

const emit = defineEmits<{
  filesSelected: [files: File[]]
  error: [message: string]
}>()

const isDragging = ref(false)
const fileInputRef = ref<HTMLInputElement | null>(null)

const acceptedTypes = computed(() => {
  if (props.accept === '*/*') return 'All files'
  return props.accept.split(',').map(t => t.trim().replace('.', '').toUpperCase()).join(', ')
})

const maxSizeFormatted = computed(() => {
  if (props.maxSize >= 1024) {
    return `${(props.maxSize / 1024).toFixed(1)} GB`
  }
  return `${props.maxSize} MB`
})

function handleDragEnter(e: DragEvent) {
  e.preventDefault()
  e.stopPropagation()
  if (!props.disabled) {
    isDragging.value = true
  }
}

function handleDragLeave(e: DragEvent) {
  e.preventDefault()
  e.stopPropagation()
  isDragging.value = false
}

function handleDragOver(e: DragEvent) {
  e.preventDefault()
  e.stopPropagation()
}

function handleDrop(e: DragEvent) {
  e.preventDefault()
  e.stopPropagation()
  isDragging.value = false

  if (props.disabled) return

  const files = e.dataTransfer?.files
  if (files) {
    processFiles(Array.from(files))
  }
}

function handleFileInputChange(e: Event) {
  const target = e.target as HTMLInputElement
  if (target.files) {
    processFiles(Array.from(target.files))
    // Reset input so same file can be selected again
    target.value = ''
  }
}

function processFiles(files: File[]) {
  const validFiles: File[] = []
  const errors: string[] = []

  // Limit number of files
  const filesToProcess = props.multiple ? files.slice(0, props.maxFiles) : [files[0]]

  if (files.length > props.maxFiles) {
    errors.push(`Maximum ${props.maxFiles} files allowed`)
  }

  for (const file of filesToProcess) {
    // Check file size
    const fileSizeMB = file.size / (1024 * 1024)
    if (fileSizeMB > props.maxSize) {
      errors.push(`${file.name} exceeds ${maxSizeFormatted.value} limit`)
      continue
    }

    // Check file type if not accepting all
    if (props.accept !== '*/*') {
      const acceptedExtensions = props.accept.split(',').map(t => t.trim().toLowerCase())
      const fileExtension = '.' + file.name.split('.').pop()?.toLowerCase()
      const mimeType = file.type.toLowerCase()

      const isAccepted = acceptedExtensions.some(accepted =>
        accepted === fileExtension ||
        accepted === mimeType ||
        (accepted.endsWith('/*') && mimeType.startsWith(accepted.replace('/*', '/')))
      )

      if (!isAccepted) {
        errors.push(`${file.name} is not an accepted file type`)
        continue
      }
    }

    validFiles.push(file)
  }

  if (errors.length > 0) {
    emit('error', errors.join('. '))
  }

  if (validFiles.length > 0) {
    emit('filesSelected', validFiles)
  }
}

function openFileDialog() {
  if (!props.disabled) {
    fileInputRef.value?.click()
  }
}

// Expose for parent components
defineExpose({
  openFileDialog
})
</script>

<template>
  <div
    class="relative"
    @dragenter="handleDragEnter"
    @dragleave="handleDragLeave"
    @dragover="handleDragOver"
    @drop="handleDrop"
  >
    <input
      ref="fileInputRef"
      type="file"
      :accept="accept"
      :multiple="multiple"
      class="hidden"
      @change="handleFileInputChange"
    />

    <div
      class="border-2 border-dashed rounded-xl p-8 text-center transition-all cursor-pointer"
      :class="[
        isDragging
          ? 'border-aegis-500 bg-aegis-50 dark:bg-aegis-900/20'
          : 'border-gray-300 dark:border-gray-600 hover:border-aegis-400 dark:hover:border-aegis-500',
        disabled
          ? 'opacity-50 cursor-not-allowed'
          : 'hover:bg-gray-50 dark:hover:bg-gray-800/50'
      ]"
      @click="openFileDialog"
    >
      <!-- Drop indicator overlay -->
      <div
        v-if="isDragging"
        class="absolute inset-0 flex items-center justify-center bg-aegis-500/10 rounded-xl z-10"
      >
        <div class="text-aegis-600 dark:text-aegis-400 text-lg font-medium">
          Drop files here
        </div>
      </div>

      <!-- Main content -->
      <div class="space-y-4">
        <div class="flex justify-center">
          <div class="p-4 bg-gray-100 dark:bg-gray-700 rounded-full">
            <CloudArrowUpIcon class="h-10 w-10 text-gray-400 dark:text-gray-500" />
          </div>
        </div>

        <div>
          <p class="text-lg font-medium text-gray-900 dark:text-gray-100">
            <span class="text-aegis-600 dark:text-aegis-400">Click to upload</span>
            or drag and drop
          </p>
          <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
            {{ acceptedTypes }} up to {{ maxSizeFormatted }}
          </p>
          <p v-if="multiple" class="text-xs text-gray-400 dark:text-gray-500 mt-1">
            Maximum {{ maxFiles }} files at once
          </p>
        </div>
      </div>
    </div>

    <!-- Slot for custom content below -->
    <slot />
  </div>
</template>
