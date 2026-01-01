<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useSessionStore } from '@/stores/session'
import { useAuthStore } from '@/stores/auth'
import { Dialog, DialogPanel, DialogTitle, TransitionChild, TransitionRoot } from '@headlessui/vue'
import {
  MagnifyingGlassIcon,
  PlusIcon,
  FunnelIcon,
  ChatBubbleLeftRightIcon,
  TrashIcon,
  ArrowDownTrayIcon
} from '@heroicons/vue/24/outline'
import type { SessionStatus, SessionType, ExportFormat } from '@/types'

const router = useRouter()
const sessionStore = useSessionStore()
const authStore = useAuthStore()

const searchQuery = ref('')
const statusFilter = ref<SessionStatus | ''>('')
const typeFilter = ref<SessionType | ''>('')
const showNewSessionDialog = ref(false)
const newSessionTitle = ref('')
const newSessionType = ref<SessionType>('QuickQuery')

const filteredSessions = computed(() => {
  let sessions = sessionStore.sessions

  if (searchQuery.value) {
    const query = searchQuery.value.toLowerCase()
    sessions = sessions.filter(s =>
      s.title.toLowerCase().includes(query) ||
      s.description?.toLowerCase().includes(query)
    )
  }

  if (statusFilter.value) {
    sessions = sessions.filter(s => s.status === statusFilter.value)
  }

  if (typeFilter.value) {
    sessions = sessions.filter(s => s.type === typeFilter.value)
  }

  return sessions
})

onMounted(() => {
  if (authStore.user) {
    sessionStore.fetchSessions({ userId: authStore.user.id })
  }
})

// Debounced search
let searchTimeout: ReturnType<typeof setTimeout>
watch(searchQuery, () => {
  clearTimeout(searchTimeout)
  searchTimeout = setTimeout(() => {
    if (authStore.user) {
      sessionStore.fetchSessions({
        userId: authStore.user.id,
        status: statusFilter.value || undefined,
        type: typeFilter.value || undefined
      })
    }
  }, 300)
})

async function createSession() {
  if (!authStore.user || !newSessionTitle.value.trim()) return

  const session = await sessionStore.createSession({
    userId: authStore.user.id,
    title: newSessionTitle.value.trim(),
    type: newSessionType.value
  })

  if (session) {
    showNewSessionDialog.value = false
    newSessionTitle.value = ''
    router.push(`/chat/${session.id}`)
  }
}

async function deleteSession(sessionId: string) {
  if (confirm('Are you sure you want to delete this session?')) {
    await sessionStore.deleteSession(sessionId)
  }
}

async function exportSession(sessionId: string, format: ExportFormat) {
  const exportData = await sessionStore.exportSession(sessionId, format)
  if (!exportData) return

  const blob = new Blob([exportData.content], { type: exportData.contentType })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = exportData.fileName
  a.click()
  URL.revokeObjectURL(url)
}

function formatDate(dateStr: string | undefined) {
  if (!dateStr) return 'N/A'
  return new Date(dateStr).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

function getTypeColor(type: SessionType): string {
  const colors: Record<SessionType, string> = {
    QuickQuery: 'bg-blue-100 text-blue-700 dark:bg-blue-900/50 dark:text-blue-300',
    Research: 'bg-purple-100 text-purple-700 dark:bg-purple-900/50 dark:text-purple-300',
    Analysis: 'bg-amber-100 text-amber-700 dark:bg-amber-900/50 dark:text-amber-300',
    Document: 'bg-green-100 text-green-700 dark:bg-green-900/50 dark:text-green-300',
    Exploration: 'bg-cyan-100 text-cyan-700 dark:bg-cyan-900/50 dark:text-cyan-300',
    Comparison: 'bg-pink-100 text-pink-700 dark:bg-pink-900/50 dark:text-pink-300'
  }
  return colors[type] || colors.QuickQuery
}

const sessionTypes: SessionType[] = ['QuickQuery', 'Research', 'Analysis', 'Document', 'Exploration', 'Comparison']
</script>

<template>
  <div class="p-6">
    <!-- Header -->
    <div class="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Sessions</h1>
        <p class="mt-1 text-gray-500 dark:text-gray-400">
          Manage your conversation history
        </p>
      </div>

      <button
        class="btn-primary gap-2"
        @click="showNewSessionDialog = true"
      >
        <PlusIcon class="h-5 w-5" />
        New Session
      </button>
    </div>

    <!-- Filters -->
    <div class="mb-6 flex flex-col gap-4 sm:flex-row">
      <!-- Search -->
      <div class="relative flex-1">
        <MagnifyingGlassIcon class="absolute left-3 top-1/2 h-5 w-5 -translate-y-1/2 text-gray-400" />
        <input
          v-model="searchQuery"
          type="text"
          class="input pl-10"
          placeholder="Search sessions..."
        />
      </div>

      <!-- Status filter -->
      <div class="flex items-center gap-2">
        <FunnelIcon class="h-5 w-5 text-gray-400" />
        <select
          v-model="statusFilter"
          class="input py-2"
        >
          <option value="">All Status</option>
          <option value="Active">Active</option>
          <option value="Ended">Ended</option>
          <option value="Archived">Archived</option>
        </select>
      </div>

      <!-- Type filter -->
      <select
        v-model="typeFilter"
        class="input py-2"
      >
        <option value="">All Types</option>
        <option v-for="type in sessionTypes" :key="type" :value="type">
          {{ type }}
        </option>
      </select>
    </div>

    <!-- Sessions list -->
    <div v-if="sessionStore.loading" class="flex items-center justify-center py-12">
      <svg class="h-8 w-8 animate-spin text-aegis-600" viewBox="0 0 24 24">
        <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" fill="none" />
        <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
      </svg>
    </div>

    <div v-else-if="filteredSessions.length === 0" class="card py-12 text-center">
      <ChatBubbleLeftRightIcon class="mx-auto h-12 w-12 text-gray-300 dark:text-gray-600" />
      <h3 class="mt-4 text-lg font-medium text-gray-900 dark:text-white">No sessions found</h3>
      <p class="mt-2 text-gray-500 dark:text-gray-400">
        {{ searchQuery || statusFilter || typeFilter ? 'Try adjusting your filters' : 'Start a new conversation to get started' }}
      </p>
      <button
        v-if="!searchQuery && !statusFilter && !typeFilter"
        class="btn-primary mt-4"
        @click="showNewSessionDialog = true"
      >
        Create your first session
      </button>
    </div>

    <div v-else class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      <div
        v-for="session in filteredSessions"
        :key="session.id"
        class="card group relative p-4 transition-shadow hover:shadow-md"
      >
        <RouterLink :to="`/chat/${session.id}`" class="block">
          <div class="mb-2 flex items-start justify-between">
            <h3 class="font-semibold text-gray-900 line-clamp-1 dark:text-white">
              {{ session.title }}
            </h3>
            <span
              class="ml-2 shrink-0 rounded-full px-2 py-0.5 text-xs font-medium"
              :class="session.status === 'Active'
                ? 'bg-green-100 text-green-700 dark:bg-green-900/50 dark:text-green-300'
                : 'bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-400'"
            >
              {{ session.status }}
            </span>
          </div>

          <p v-if="session.description" class="mb-3 text-sm text-gray-500 line-clamp-2 dark:text-gray-400">
            {{ session.description }}
          </p>

          <div class="flex items-center gap-2 text-xs text-gray-400">
            <span
              class="rounded-full px-2 py-0.5 font-medium"
              :class="getTypeColor(session.type)"
            >
              {{ session.type }}
            </span>
            <span>{{ session.turnCount }} turns</span>
            <span>·</span>
            <span>{{ formatDate(session.lastActivityAt || session.createdAt) }}</span>
          </div>

          <!-- Tags -->
          <div v-if="session.tags?.length" class="mt-2 flex flex-wrap gap-1">
            <span
              v-for="tag in session.tags.slice(0, 3)"
              :key="tag"
              class="rounded bg-gray-100 px-1.5 py-0.5 text-xs text-gray-600 dark:bg-gray-700 dark:text-gray-400"
            >
              {{ tag }}
            </span>
            <span v-if="session.tags.length > 3" class="text-xs text-gray-400">
              +{{ session.tags.length - 3 }}
            </span>
          </div>
        </RouterLink>

        <!-- Actions (visible on hover) -->
        <div class="absolute right-2 top-2 flex gap-1 opacity-0 transition-opacity group-hover:opacity-100">
          <button
            class="rounded p-1 hover:bg-gray-100 dark:hover:bg-gray-700"
            title="Export"
            @click.prevent="exportSession(session.id, 'Markdown' as ExportFormat)"
          >
            <ArrowDownTrayIcon class="h-4 w-4 text-gray-400" />
          </button>
          <button
            class="rounded p-1 hover:bg-red-50 dark:hover:bg-red-900/30"
            title="Delete"
            @click.prevent="deleteSession(session.id)"
          >
            <TrashIcon class="h-4 w-4 text-red-400" />
          </button>
        </div>
      </div>
    </div>

    <!-- Pagination -->
    <div v-if="sessionStore.hasMore" class="mt-6 text-center">
      <button
        class="btn-secondary"
        :disabled="sessionStore.loading"
        @click="sessionStore.fetchSessions({ page: sessionStore.currentPage + 1 })"
      >
        Load more
      </button>
    </div>

    <!-- New Session Dialog -->
    <TransitionRoot appear :show="showNewSessionDialog" as="template">
      <Dialog as="div" class="relative z-50" @close="showNewSessionDialog = false">
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
              <DialogPanel class="w-full max-w-md transform overflow-hidden rounded-2xl bg-white p-6 shadow-xl transition-all dark:bg-gray-800">
                <DialogTitle as="h3" class="text-lg font-semibold text-gray-900 dark:text-white">
                  Create New Session
                </DialogTitle>

                <form @submit.prevent="createSession" class="mt-4 space-y-4">
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">
                      Session Title
                    </label>
                    <input
                      v-model="newSessionTitle"
                      type="text"
                      class="input mt-1"
                      placeholder="Enter a title for your session"
                      required
                    />
                  </div>

                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300">
                      Session Type
                    </label>
                    <select v-model="newSessionType" class="input mt-1">
                      <option v-for="type in sessionTypes" :key="type" :value="type">
                        {{ type }}
                      </option>
                    </select>
                  </div>

                  <div class="flex justify-end gap-3 pt-4">
                    <button
                      type="button"
                      class="btn-secondary"
                      @click="showNewSessionDialog = false"
                    >
                      Cancel
                    </button>
                    <button
                      type="submit"
                      class="btn-primary"
                      :disabled="!newSessionTitle.trim()"
                    >
                      Create Session
                    </button>
                  </div>
                </form>
              </DialogPanel>
            </TransitionChild>
          </div>
        </div>
      </Dialog>
    </TransitionRoot>
  </div>
</template>
