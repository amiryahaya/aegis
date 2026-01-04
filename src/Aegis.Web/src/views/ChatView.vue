<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useSessionStore } from '@/stores/session'
import { useWorkspaceStore } from '@/stores/workspace'
import { useAuthStore } from '@/stores/auth'
import ChatMessage from '@/components/chat/ChatMessage.vue'
import ChatInput from '@/components/chat/ChatInput.vue'
import LivePresence from '@/components/connection/LivePresence.vue'
import TypingIndicator from '@/components/connection/TypingIndicator.vue'
import MobileChatHeader from '@/components/mobile/MobileChatHeader.vue'
import MobileChatInput from '@/components/mobile/MobileChatInput.vue'
import MobileMessageBubble from '@/components/mobile/MobileMessageBubble.vue'
import MobileSourcesSheet from '@/components/mobile/MobileSourcesSheet.vue'
import SessionShareDialog from '@/components/session/SessionShareDialog.vue'
import { useQuery } from '@/composables/useQuery'
import { useConnection } from '@/composables/useConnection'
import { useBreakpoints } from '@/composables/useMediaQuery'
import { useToast } from '@/composables/useToast'
import {
  PencilIcon,
  TrashIcon,
  ArrowDownTrayIcon,
  EllipsisVerticalIcon,
  FolderIcon,
  ShareIcon
} from '@heroicons/vue/24/outline'
import { Menu, MenuButton, MenuItem, MenuItems, Listbox, ListboxButton, ListboxOptions, ListboxOption } from '@headlessui/vue'
import { SessionType, type ExportFormat, type SourceReference } from '@/types'

const route = useRoute()
const router = useRouter()
const sessionStore = useSessionStore()
const workspaceStore = useWorkspaceStore()
const authStore = useAuthStore()
const { joinResource, leaveResource, sendTypingIndicator } = useConnection()
const { isMobile } = useBreakpoints()
const toast = useToast()

// Selected workspace for queries
const selectedWorkspaceId = ref<string | null>(null)

const chatContainerRef = ref<HTMLDivElement>()
const chatInputRef = ref<InstanceType<typeof ChatInput>>()
const mobileChatInputRef = ref<InstanceType<typeof MobileChatInput>>()
const streamingTurnId = ref<string | null>(null)
const isEditingTitle = ref(false)
const editTitle = ref('')

// Mobile-specific state
const showSourcesSheet = ref(false)
const selectedSources = ref<SourceReference[]>([])

// Share dialog state
const isShareDialogOpen = ref(false)

const sessionId = computed(() => route.params.sessionId as string | undefined)

const currentSession = computed(() => sessionStore.currentSession)
const turns = computed(() => sessionStore.currentTurns)
const workspaces = computed(() => workspaceStore.workspaces)
const selectedWorkspace = computed(() =>
  workspaces.value.find(w => w.id === selectedWorkspaceId.value)
)

// Create query composable when workspace is selected
const queryComposable = computed(() => {
  if (selectedWorkspaceId.value) {
    return useQuery(selectedWorkspaceId.value, {
      onToken: () => scrollToBottom(),
      onComplete: () => scrollToBottom()
    })
  }
  return null
})

const isLoading = computed(() => queryComposable.value?.isLoading.value ?? false)
const isStreaming = computed(() => queryComposable.value?.isStreaming.value ?? false)

// Load session on mount or route change
watch(sessionId, async (id, oldId) => {
  // Leave previous session
  if (oldId) {
    leaveResource('session', oldId)
  }

  if (id) {
    const session = await sessionStore.fetchSession(id)
    await sessionStore.fetchTurns(id)
    scrollToBottom()
    // Join new session for presence
    joinResource('session', id)
    // Set workspace from session
    if (session?.workspaceId) {
      selectedWorkspaceId.value = session.workspaceId
    }
  } else {
    sessionStore.clearCurrent()
  }
}, { immediate: true })

onMounted(async () => {
  chatInputRef.value?.focus()
  // Load available workspaces
  if (authStore.user?.teamId) {
    await workspaceStore.fetchWorkspaces(authStore.user.teamId)
    // Select first workspace if none selected
    if (!selectedWorkspaceId.value && workspaces.value.length > 0) {
      selectedWorkspaceId.value = workspaces.value[0].id
    }
  }
})

onUnmounted(() => {
  // Cancel any streaming query
  queryComposable.value?.cancel()
  // Leave session presence
  if (sessionId.value) {
    leaveResource('session', sessionId.value)
  }
})

// Handle typing indicator
let typingTimeout: ReturnType<typeof setTimeout> | null = null
function handleTyping() {
  if (!sessionId.value) return

  sendTypingIndicator(sessionId.value, true)

  // Clear existing timeout
  if (typingTimeout) {
    clearTimeout(typingTimeout)
  }

  // Stop typing after 2 seconds of inactivity
  typingTimeout = setTimeout(() => {
    if (sessionId.value) {
      sendTypingIndicator(sessionId.value, false)
    }
  }, 2000)
}

// Watch for streaming completion to save the turn
watch(isStreaming, async (streaming, wasStreaming) => {
  if (wasStreaming && !streaming && streamingTurnId.value && queryComposable.value) {
    // Stream complete - save the response
    const sid = sessionId.value
    const qc = queryComposable.value
    if (sid && qc.response.value) {
      const sources = qc.sources.value.map(s => ({
        documentId: s.documentId,
        documentName: s.documentName,
        relevanceScore: s.relevance,
        excerpt: s.content?.slice(0, 200)
      }))

      await sessionStore.completeTurn(
        sid,
        streamingTurnId.value,
        qc.response.value,
        sources,
        qc.followUpQuestions.value
      )
    }

    streamingTurnId.value = null
    scrollToBottom()
  }
})

async function handleSend(query: string) {
  if (!authStore.user) {
    toast.error('Not authenticated', 'Please log in to send messages')
    return
  }
  if (!selectedWorkspaceId.value) {
    toast.warning('No workspace selected', 'Please select a workspace to query')
    return
  }

  try {
    // Create session if none exists
    let sid = sessionId.value
    if (!sid) {
      const session = await sessionStore.createSession({
        userId: authStore.user.id,
        workspaceId: selectedWorkspaceId.value,
        title: query.slice(0, 50) + (query.length > 50 ? '...' : ''),
        type: SessionType.QuickQuery
      })
      if (!session) {
        toast.error('Session creation failed', 'Unable to create a new session')
        return
      }
      sid = session.id
      router.replace(`/chat/${sid}`)
    }

    // Add the turn (user query)
    const turn = await sessionStore.addTurn(sid, { query })
    if (!turn) {
      toast.error('Failed to send message', 'Unable to add your message to the session')
      return
    }

    streamingTurnId.value = turn.id
    scrollToBottom()

    // Use the query composable for streaming
    if (queryComposable.value) {
      queryComposable.value.streamQuery(query)
    }
  } catch (error) {
    toast.apiError(error, 'Error sending message')
  }
}

function handleCancel() {
  queryComposable.value?.cancel()
  streamingTurnId.value = null
}

function handleFollowUp(question: string) {
  if (isMobile.value) {
    mobileChatInputRef.value?.setQuery(question)
  } else {
    chatInputRef.value?.setQuery(question)
    chatInputRef.value?.focus()
  }
}

function openSourcesSheet(sources: SourceReference[]) {
  selectedSources.value = sources
  showSourcesSheet.value = true
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

  try {
    await sessionStore.updateTitle(sessionId.value, editTitle.value.trim())
    isEditingTitle.value = false
    toast.success('Title updated', 'Session title has been updated')
  } catch (error) {
    toast.apiError(error, 'Failed to update title')
  }
}

async function handleDelete() {
  if (!sessionId.value) return

  try {
    await sessionStore.deleteSession(sessionId.value)
    toast.success('Session deleted', 'The session has been removed')
    router.push('/sessions')
  } catch (error) {
    toast.apiError(error, 'Failed to delete session')
  }
}

function confirmDelete() {
  if (confirm('Are you sure you want to delete this session? This cannot be undone.')) {
    handleDelete()
  }
}

async function handleExport(format: ExportFormat) {
  if (!sessionId.value) return

  try {
    const exportData = await sessionStore.exportSession(sessionId.value, format)
    if (!exportData) {
      toast.error('Export failed', 'Unable to export session data')
      return
    }

    // Create download
    const blob = new Blob([exportData.content], { type: exportData.contentType })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = exportData.fileName
    a.click()
    URL.revokeObjectURL(url)

    toast.success('Export complete', `Session exported as ${format}`)
  } catch (error) {
    toast.apiError(error, 'Export failed')
  }
}
</script>

<template>
  <div class="flex h-full flex-col">
    <!-- Mobile Header -->
    <MobileChatHeader
      v-if="currentSession && isMobile"
      :title="currentSession.title"
      :subtitle="`${currentSession.turnCount} turns`"
      :is-editing="isEditingTitle"
      v-model:edit-title="editTitle"
      @edit="startEditTitle"
      @save-title="saveTitle"
      @delete="confirmDelete"
      @export="(format: string) => handleExport(format as ExportFormat)"
    />

    <!-- Desktop Header -->
    <div v-if="currentSession && !isMobile" class="hidden md:flex items-center justify-between border-b border-gray-200 bg-white px-4 py-3 dark:border-gray-700 dark:bg-gray-800">
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

      <div class="flex items-center gap-3">
        <!-- Live presence for this session -->
        <LivePresence
          v-if="sessionId"
          resource-type="session"
          :resource-id="sessionId"
          :max-avatars="3"
          :show-count="false"
        />

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
                  class="flex w-full items-center gap-2 px-4 py-2 text-sm"
                  :class="active ? 'bg-gray-100 dark:bg-gray-700' : ''"
                  @click="isShareDialogOpen = true"
                >
                  <ShareIcon class="h-4 w-4" />
                  Share session
                </button>
              </MenuItem>
              <div class="my-1 border-t border-gray-200 dark:border-gray-700" />
              <MenuItem v-slot="{ active }">
                <button
                  class="flex w-full items-center gap-2 px-4 py-2 text-sm text-red-600 dark:text-red-400"
                  :class="active ? 'bg-red-50 dark:bg-red-900/30' : ''"
                  @click="confirmDelete"
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
      <!-- Empty state -->
      <div
        v-if="!currentSession || turns.length === 0"
        class="flex h-full flex-col items-center justify-center px-4 py-12 text-center"
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

      <!-- Desktop Messages -->
      <div v-else-if="!isMobile" class="mx-auto max-w-3xl px-4 py-6">
        <div class="space-y-6">
          <ChatMessage
            v-for="turn in turns"
            :key="turn.id"
            :turn="turn"
            :is-streaming="turn.id === streamingTurnId"
            @ask-follow-up="handleFollowUp"
          />
        </div>
      </div>

      <!-- Mobile Messages -->
      <div v-else class="py-4 space-y-4">
        <MobileMessageBubble
          v-for="turn in turns"
          :key="turn.id"
          :turn="turn"
          :is-streaming="turn.id === streamingTurnId"
          @ask-follow-up="handleFollowUp"
          @view-sources="turn.sources && openSourcesSheet(turn.sources)"
        />
      </div>
    </div>

    <!-- Typing indicator -->
    <TypingIndicator
      v-if="sessionId"
      :session-id="sessionId"
    />

    <!-- Workspace Selector (when no session) -->
    <div
      v-if="!currentSession && workspaces.length > 0"
      class="border-t border-gray-200 bg-gray-50 px-4 py-2 dark:border-gray-700 dark:bg-gray-800/50"
    >
      <div class="mx-auto flex max-w-3xl items-center gap-2">
        <FolderIcon class="h-4 w-4 text-gray-500" />
        <span class="text-sm text-gray-500 dark:text-gray-400">Query workspace:</span>
        <Listbox v-model="selectedWorkspaceId">
          <div class="relative flex-1">
            <ListboxButton class="relative w-full cursor-pointer rounded-lg bg-white py-1.5 pl-3 pr-10 text-left text-sm shadow-sm ring-1 ring-gray-300 focus:outline-none focus:ring-2 focus:ring-aegis-500 dark:bg-gray-700 dark:ring-gray-600">
              <span class="block truncate">{{ selectedWorkspace?.name || 'Select a workspace' }}</span>
            </ListboxButton>
            <ListboxOptions class="absolute z-10 mt-1 max-h-60 w-full overflow-auto rounded-lg bg-white py-1 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-700">
              <ListboxOption
                v-for="workspace in workspaces"
                :key="workspace.id"
                :value="workspace.id"
                as="template"
                v-slot="{ active: isActive, selected: isSelected }"
              >
                <li
                  class="cursor-pointer select-none px-3 py-2"
                  :class="[
                    isActive ? 'bg-aegis-100 dark:bg-aegis-900/50' : '',
                    isSelected ? 'font-semibold' : ''
                  ]"
                >
                  {{ workspace.name }}
                </li>
              </ListboxOption>
            </ListboxOptions>
          </div>
        </Listbox>
      </div>
    </div>

    <!-- Desktop Input -->
    <ChatInput
      v-if="!isMobile"
      ref="chatInputRef"
      :loading="isLoading"
      :disabled="!selectedWorkspaceId"
      class="hidden md:block"
      @send="handleSend"
      @stop="handleCancel"
      @input="handleTyping"
    />

    <!-- Mobile Input -->
    <MobileChatInput
      v-if="isMobile"
      ref="mobileChatInputRef"
      :loading="isLoading"
      :disabled="!selectedWorkspaceId"
      @send="handleSend"
      @stop="handleCancel"
      @input="handleTyping"
    />

    <!-- Mobile Sources Sheet -->
    <MobileSourcesSheet
      :sources="selectedSources"
      :open="showSourcesSheet"
      @close="showSourcesSheet = false"
    />

    <!-- Session Share Dialog -->
    <SessionShareDialog
      :session="currentSession"
      :open="isShareDialogOpen"
      @close="isShareDialogOpen = false"
      @shared="isShareDialogOpen = false"
    />
  </div>
</template>
