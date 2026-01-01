<script setup lang="ts">
import { ref, computed, onMounted, nextTick, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useSessionStore } from '@/stores/session'
import { useAuthStore } from '@/stores/auth'
import ChatMessage from '@/components/chat/ChatMessage.vue'
import ChatInput from '@/components/chat/ChatInput.vue'
import {
  PencilIcon,
  TrashIcon,
  ArrowDownTrayIcon,
  EllipsisVerticalIcon
} from '@heroicons/vue/24/outline'
import { Menu, MenuButton, MenuItem, MenuItems } from '@headlessui/vue'
import type { SessionTurn, ExportFormat } from '@/types'

const route = useRoute()
const router = useRouter()
const sessionStore = useSessionStore()
const authStore = useAuthStore()

const chatContainerRef = ref<HTMLDivElement>()
const chatInputRef = ref<InstanceType<typeof ChatInput>>()
const isLoading = ref(false)
const streamingTurnId = ref<string | null>(null)
const isEditingTitle = ref(false)
const editTitle = ref('')

const sessionId = computed(() => route.params.sessionId as string | undefined)

const currentSession = computed(() => sessionStore.currentSession)
const turns = computed(() => sessionStore.currentTurns)

// Load session on mount or route change
watch(sessionId, async (id) => {
  if (id) {
    await sessionStore.fetchSession(id)
    await sessionStore.fetchTurns(id)
    scrollToBottom()
  } else {
    sessionStore.clearCurrent()
  }
}, { immediate: true })

onMounted(() => {
  chatInputRef.value?.focus()
})

async function handleSend(query: string) {
  if (!authStore.user) return

  // Create session if none exists
  let sid = sessionId.value
  if (!sid) {
    const session = await sessionStore.createSession({
      userId: authStore.user.id,
      title: query.slice(0, 50) + (query.length > 50 ? '...' : ''),
      type: 'QuickQuery'
    })
    if (!session) return
    sid = session.id
    router.replace(`/chat/${sid}`)
  }

  isLoading.value = true

  // Add the turn (user query)
  const turn = await sessionStore.addTurn(sid, { query })
  if (!turn) {
    isLoading.value = false
    return
  }

  streamingTurnId.value = turn.id
  scrollToBottom()

  // Simulate streaming response (in real implementation, use SignalR)
  // For now, we'll call the complete endpoint
  try {
    // In a real implementation, this would connect to SignalR for streaming
    // For demo, we simulate with a timeout and mock response
    await new Promise(resolve => setTimeout(resolve, 1500))

    const mockResponse = `Based on my analysis of the available documents, here's what I found regarding your query: "${query}"

This is a simulated response. In the full implementation, this would be streamed from the backend using SignalR, providing real-time token-by-token output as the LLM generates the response.

The response would include:
- Relevant information from your documents
- Citations to source materials
- Follow-up questions to explore the topic further`

    await sessionStore.completeTurn(
      sid,
      turn.id,
      mockResponse,
      [
        { documentId: '1', documentName: 'sample-doc.pdf', relevanceScore: 0.92, excerpt: 'Relevant excerpt from the document...' }
      ],
      ['What are the key takeaways?', 'Can you provide more details?']
    )
  } finally {
    isLoading.value = false
    streamingTurnId.value = null
    scrollToBottom()
  }
}

function handleFollowUp(question: string) {
  chatInputRef.value?.setQuery(question)
  chatInputRef.value?.focus()
}

function scrollToBottom() {
  nextTick(() => {
    if (chatContainerRef.value) {
      chatContainerRef.value.scrollTop = chatContainerRef.value.scrollHeight
    }
  })
}

function startEditTitle() {
  if (!currentSession.value) return
  editTitle.value = currentSession.value.title
  isEditingTitle.value = true
}

async function saveTitle() {
  if (!sessionId.value || !editTitle.value.trim()) return

  await sessionStore.updateTitle(sessionId.value, editTitle.value.trim())
  isEditingTitle.value = false
}

async function handleDelete() {
  if (!sessionId.value) return

  if (confirm('Are you sure you want to delete this session?')) {
    await sessionStore.deleteSession(sessionId.value)
    router.push('/sessions')
  }
}

async function handleExport(format: ExportFormat) {
  if (!sessionId.value) return

  const exportData = await sessionStore.exportSession(sessionId.value, format)
  if (!exportData) return

  // Create download
  const blob = new Blob([exportData.content], { type: exportData.contentType })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = exportData.fileName
  a.click()
  URL.revokeObjectURL(url)
}
</script>

<template>
  <div class="flex h-full flex-col">
    <!-- Header -->
    <div v-if="currentSession" class="flex items-center justify-between border-b border-gray-200 bg-white px-4 py-3 dark:border-gray-700 dark:bg-gray-800">
      <div class="flex items-center gap-2 min-w-0">
        <template v-if="isEditingTitle">
          <input
            v-model="editTitle"
            class="input py-1 text-lg font-semibold"
            @keydown.enter="saveTitle"
            @keydown.escape="isEditingTitle = false"
            @blur="saveTitle"
          />
        </template>
        <template v-else>
          <h1 class="truncate text-lg font-semibold text-gray-900 dark:text-white">
            {{ currentSession.title }}
          </h1>
          <button
            class="btn-ghost p-1"
            @click="startEditTitle"
          >
            <PencilIcon class="h-4 w-4" />
          </button>
        </template>
      </div>

      <div class="flex items-center gap-2">
        <span class="text-sm text-gray-500 dark:text-gray-400">
          {{ currentSession.turnCount }} turns
        </span>

        <Menu as="div" class="relative">
          <MenuButton class="btn-ghost p-2">
            <EllipsisVerticalIcon class="h-5 w-5" />
          </MenuButton>

          <transition
            enter-active-class="transition ease-out duration-100"
            enter-from-class="transform opacity-0 scale-95"
            enter-to-class="transform opacity-100 scale-100"
            leave-active-class="transition ease-in duration-75"
            leave-from-class="transform opacity-100 scale-100"
            leave-to-class="transform opacity-0 scale-95"
          >
            <MenuItems class="absolute right-0 mt-2 w-48 origin-top-right rounded-lg bg-white py-1 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-800 dark:ring-gray-700">
              <MenuItem v-slot="{ active }">
                <button
                  class="flex w-full items-center gap-2 px-4 py-2 text-sm"
                  :class="active ? 'bg-gray-100 dark:bg-gray-700' : ''"
                  @click="handleExport('Markdown' as ExportFormat)"
                >
                  <ArrowDownTrayIcon class="h-4 w-4" />
                  Export as Markdown
                </button>
              </MenuItem>
              <MenuItem v-slot="{ active }">
                <button
                  class="flex w-full items-center gap-2 px-4 py-2 text-sm"
                  :class="active ? 'bg-gray-100 dark:bg-gray-700' : ''"
                  @click="handleExport('Json' as ExportFormat)"
                >
                  <ArrowDownTrayIcon class="h-4 w-4" />
                  Export as JSON
                </button>
              </MenuItem>
              <MenuItem v-slot="{ active }">
                <button
                  class="flex w-full items-center gap-2 px-4 py-2 text-sm text-red-600 dark:text-red-400"
                  :class="active ? 'bg-red-50 dark:bg-red-900/30' : ''"
                  @click="handleDelete"
                >
                  <TrashIcon class="h-4 w-4" />
                  Delete session
                </button>
              </MenuItem>
            </MenuItems>
          </transition>
        </Menu>
      </div>
    </div>

    <!-- Chat messages -->
    <div
      ref="chatContainerRef"
      class="flex-1 overflow-y-auto scrollbar-thin"
    >
      <div class="mx-auto max-w-3xl px-4 py-6">
        <!-- Empty state -->
        <div
          v-if="!currentSession || turns.length === 0"
          class="flex h-full flex-col items-center justify-center py-12 text-center"
        >
          <div class="rounded-full bg-aegis-100 p-4 dark:bg-aegis-900/50">
            <svg class="h-12 w-12 text-aegis-600 dark:text-aegis-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z" />
            </svg>
          </div>
          <h2 class="mt-4 text-xl font-semibold text-gray-900 dark:text-white">
            Start a conversation
          </h2>
          <p class="mt-2 max-w-sm text-gray-500 dark:text-gray-400">
            Ask questions about your documents and I'll help you find the answers.
          </p>
        </div>

        <!-- Messages -->
        <div v-else class="space-y-6">
          <ChatMessage
            v-for="turn in turns"
            :key="turn.id"
            :turn="turn"
            :is-streaming="turn.id === streamingTurnId"
            @ask-follow-up="handleFollowUp"
          />
        </div>
      </div>
    </div>

    <!-- Input -->
    <ChatInput
      ref="chatInputRef"
      :loading="isLoading"
      @send="handleSend"
      @stop="isLoading = false"
    />
  </div>
</template>
