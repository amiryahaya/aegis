<script setup lang="ts">
import { computed } from 'vue'
import { useConnection } from '@/composables/useConnection'

interface Props {
  sessionId: string
}

const props = defineProps<Props>()

const { getTypingUsers } = useConnection()

const typingUsers = computed(() => getTypingUsers(props.sessionId))

const typingMessage = computed(() => {
  const users = typingUsers.value
  if (users.length === 0) return ''
  if (users.length === 1) return `${users[0].userName} is typing...`
  if (users.length === 2) return `${users[0].userName} and ${users[1].userName} are typing...`
  return `${users[0].userName} and ${users.length - 1} others are typing...`
})
</script>

<template>
  <transition
    enter-active-class="transition duration-200 ease-out"
    enter-from-class="opacity-0 translate-y-1"
    enter-to-class="opacity-100 translate-y-0"
    leave-active-class="transition duration-150 ease-in"
    leave-from-class="opacity-100 translate-y-0"
    leave-to-class="opacity-0 translate-y-1"
  >
    <div
      v-if="typingUsers.length > 0"
      class="flex items-center gap-2 px-4 py-2 text-sm text-gray-500 dark:text-gray-400"
    >
      <!-- Animated dots -->
      <div class="flex items-center gap-0.5">
        <span
          class="h-1.5 w-1.5 animate-bounce rounded-full bg-gray-400 dark:bg-gray-500"
          style="animation-delay: 0ms"
        />
        <span
          class="h-1.5 w-1.5 animate-bounce rounded-full bg-gray-400 dark:bg-gray-500"
          style="animation-delay: 150ms"
        />
        <span
          class="h-1.5 w-1.5 animate-bounce rounded-full bg-gray-400 dark:bg-gray-500"
          style="animation-delay: 300ms"
        />
      </div>

      <!-- Message -->
      <span>{{ typingMessage }}</span>
    </div>
  </transition>
</template>
