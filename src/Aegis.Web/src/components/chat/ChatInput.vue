<script setup lang="ts">
import { ref, watch } from 'vue'
import { PaperAirplaneIcon, StopIcon } from '@heroicons/vue/24/solid'

const props = defineProps<{
  disabled?: boolean
  loading?: boolean
}>()

const emit = defineEmits<{
  send: [query: string]
  stop: []
}>()

const query = ref('')
const textareaRef = ref<HTMLTextAreaElement>()

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
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault()
    handleSubmit()
  }
}

// Auto-resize textarea
watch(query, () => {
  if (textareaRef.value) {
    textareaRef.value.style.height = 'auto'
    textareaRef.value.style.height = `${Math.min(textareaRef.value.scrollHeight, 200)}px`
  }
})

defineExpose({
  focus: () => textareaRef.value?.focus(),
  setQuery: (text: string) => {
    query.value = text
  }
})
</script>

<template>
  <div class="border-t border-gray-200 bg-white p-4 dark:border-gray-700 dark:bg-gray-800">
    <div class="mx-auto max-w-3xl">
      <div class="relative flex items-end gap-2">
        <div class="relative flex-1">
          <textarea
            ref="textareaRef"
            v-model="query"
            :disabled="disabled"
            rows="1"
            class="input min-h-[44px] max-h-[200px] resize-none pr-12 py-3"
            placeholder="Ask anything about your documents..."
            @keydown="handleKeydown"
          />
        </div>

        <button
          v-if="loading"
          type="button"
          class="btn-secondary shrink-0 p-3"
          @click="emit('stop')"
        >
          <StopIcon class="h-5 w-5" />
        </button>
        <button
          v-else
          type="button"
          :disabled="!query.trim() || disabled"
          class="btn-primary shrink-0 p-3"
          @click="handleSubmit"
        >
          <PaperAirplaneIcon class="h-5 w-5" />
        </button>
      </div>

      <p class="mt-2 text-center text-xs text-gray-400">
        Press Enter to send, Shift+Enter for new line
      </p>
    </div>
  </div>
</template>
