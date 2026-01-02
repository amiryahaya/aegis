<script setup lang="ts">
import { ref, watch, nextTick } from 'vue'
import {
  PaperAirplaneIcon,
  StopIcon,
  MicrophoneIcon,
  PlusIcon
} from '@heroicons/vue/24/solid'

const props = defineProps<{
  disabled?: boolean
  loading?: boolean
  showAttach?: boolean
}>()

const emit = defineEmits<{
  send: [query: string]
  stop: []
  input: []
  attach: []
  voice: []
}>()

const query = ref('')
const textareaRef = ref<HTMLTextAreaElement>()
const isFocused = ref(false)

function handleSubmit() {
  if (!query.value.trim() || props.loading) return

  emit('send', query.value.trim())
  query.value = ''

  // Reset textarea height
  if (textareaRef.value) {
    textareaRef.value.style.height = 'auto'
  }
}

function handleKeydown(e: KeyboardEvent) {
  // On mobile, Enter should insert newline by default
  // Submit only with dedicated button
  if (e.key === 'Enter' && e.ctrlKey) {
    e.preventDefault()
    handleSubmit()
  }
}

// Auto-resize textarea
watch(query, () => {
  nextTick(() => {
    if (textareaRef.value) {
      textareaRef.value.style.height = 'auto'
      textareaRef.value.style.height = `${Math.min(textareaRef.value.scrollHeight, 120)}px`
    }
  })
})

function focus() {
  textareaRef.value?.focus()
}

function setQuery(text: string) {
  query.value = text
  focus()
}

defineExpose({ focus, setQuery })
</script>

<template>
  <div
    class="border-t border-gray-200 bg-white dark:border-gray-700 dark:bg-gray-800 safe-area-bottom md:hidden"
  >
    <div class="flex items-end gap-2 px-3 py-2">
      <!-- Attach button -->
      <button
        v-if="showAttach"
        type="button"
        class="flex h-10 w-10 shrink-0 items-center justify-center rounded-full text-gray-500 hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-700"
        @click="emit('attach')"
      >
        <PlusIcon class="h-5 w-5" />
      </button>

      <!-- Input container -->
      <div
        class="relative flex flex-1 items-end rounded-2xl border bg-gray-50 transition-colors dark:bg-gray-700"
        :class="isFocused ? 'border-aegis-500' : 'border-gray-200 dark:border-gray-600'"
      >
        <textarea
          ref="textareaRef"
          v-model="query"
          :disabled="disabled"
          rows="1"
          class="flex-1 resize-none bg-transparent px-4 py-2.5 text-base text-gray-900 placeholder:text-gray-400 focus:outline-none dark:text-white dark:placeholder:text-gray-500"
          style="max-height: 120px; min-height: 40px"
          placeholder="Message..."
          @keydown="handleKeydown"
          @input="emit('input')"
          @focus="isFocused = true"
          @blur="isFocused = false"
        />

        <!-- Voice button (when empty) -->
        <button
          v-if="!query.trim() && !loading"
          type="button"
          class="mr-1 flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-gray-400 hover:text-gray-600 dark:hover:text-gray-300"
          @click="emit('voice')"
        >
          <MicrophoneIcon class="h-5 w-5" />
        </button>
      </div>

      <!-- Send/Stop button -->
      <button
        v-if="loading"
        type="button"
        class="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-red-500 text-white shadow-sm transition-transform active:scale-95"
        @click="emit('stop')"
      >
        <StopIcon class="h-5 w-5" />
      </button>
      <button
        v-else
        type="button"
        :disabled="!query.trim() || disabled"
        class="flex h-10 w-10 shrink-0 items-center justify-center rounded-full shadow-sm transition-all active:scale-95"
        :class="query.trim()
          ? 'bg-aegis-600 text-white'
          : 'bg-gray-100 text-gray-400 dark:bg-gray-700'"
        @click="handleSubmit"
      >
        <PaperAirplaneIcon class="h-5 w-5" />
      </button>
    </div>
  </div>
</template>

<style scoped>
.safe-area-bottom {
  padding-bottom: max(0.5rem, env(safe-area-inset-bottom, 0px));
}
</style>
