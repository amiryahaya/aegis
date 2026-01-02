<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import { z } from 'zod'
import { useSessionStore } from '@/stores/session'
import { useAuthStore } from '@/stores/auth'
import { useBulkSelection } from '@/composables/useBulkSelection'
import { useBreakpoints } from '@/composables/useMediaQuery'
import { usePullToRefresh } from '@/composables/useTouchGestures'
import { Dialog, DialogPanel, DialogTitle, TransitionChild, TransitionRoot } from '@headlessui/vue'
import {
  MagnifyingGlassIcon,
  PlusIcon,
  FunnelIcon,
  ChatBubbleLeftRightIcon,
  TrashIcon,
  ArrowDownTrayIcon,
  CheckIcon
} from '@heroicons/vue/24/outline'
import { SessionType, type SessionStatus, type ExportFormat, type BulkAction } from '@/types'
import { FormField } from '@/components/form'
import BulkActionsToolbar from '@/components/common/BulkActionsToolbar.vue'
import FloatingActionButton from '@/components/mobile/FloatingActionButton.vue'
import PullToRefreshIndicator from '@/components/mobile/PullToRefreshIndicator.vue'

const router = useRouter()
const sessionStore = useSessionStore()
const authStore = useAuthStore()
const { isMobile } = useBreakpoints()

// Pull to refresh
const sessionsListRef = ref<HTMLElement | null>(null)
const pullToRefresh = usePullToRefresh(sessionsListRef, {
  threshold: 80,
  onRefresh: async () => {
    await sessionStore.fetchSessions({ page: 1 })
  }
})

// Bulk selection
const bulkSelection = useBulkSelection({
  items: () => filteredSessions.value,
  getId: (session) => session.id,
  onAction: handleBulkAction
})

const sessionBulkActions: BulkAction[] = [
  {
    id: 'export',
    label: 'Export',
    icon: 'ArrowDownTrayIcon',
    variant: 'default'
  },
  {
    id: 'archive',
    label: 'Archive',
    icon: 'ArchiveBoxIcon',
    variant: 'warning',
    requiresConfirmation: true,
    confirmationMessage: 'Are you sure you want to archive the selected sessions?'
  },
  {
    id: 'delete',
    label: 'Delete',
    icon: 'TrashIcon',
    variant: 'danger',
    requiresConfirmation: true,
    confirmationMessage: 'Are you sure you want to delete the selected sessions? This action cannot be undone.'
  }
]

async function handleBulkAction(action: BulkAction, selectedIds: string[]): Promise<void> {
  switch (action.id) {
    case 'delete':
      for (const id of selectedIds) {
        await sessionStore.deleteSession(id)
      }
      break
    case 'export':
      for (const id of selectedIds) {
        await exportSession(id, 'Markdown' as ExportFormat)
      }
      break
    case 'archive':
      // Archive implementation would go here
      console.log('Archive sessions:', selectedIds)
      break
  }
}

const searchQuery = ref('')
const statusFilter = ref<SessionStatus | ''>('')
const typeFilter = ref<SessionType | ''>('')
const showNewSessionDialog = ref(false)

// Form validation schema for new session
const createSessionSchema = z.object({
  title: z
    .string()
    .min(1, 'Session title is required')
    .min(3, 'Title must be at least 3 characters')
    .max(200, 'Title must be less than 200 characters'),
  type: z.nativeEnum(SessionType)
})

type CreateSessionFormData = z.infer<typeof createSessionSchema>

const { defineField, handleSubmit, errors, resetForm, meta } = useForm<CreateSessionFormData>({
  validationSchema: toTypedSchema(createSessionSchema),
  initialValues: {
    title: '',
    type: SessionType.QuickQuery
  }
})

const [title] = defineField('title')
const [sessionType] = defineField('type')
const titleTouched = ref(false)

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

// Reset form when dialog is closed
watch(showNewSessionDialog, (isOpen) => {
  if (!isOpen) {
    resetForm()
    titleTouched.value = false
  }
})

const createSession = handleSubmit(async (values) => {
  if (!authStore.user) return

  const session = await sessionStore.createSession({
    userId: authStore.user.id,
    title: values.title.trim(),
    type: values.type
  })

  if (session) {
    showNewSessionDialog.value = false
    router.push(`/chat/${session.id}`)
  }
})

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

const sessionTypes: SessionType[] = [
  SessionType.QuickQuery,
  SessionType.Research,
  SessionType.Analysis,
  SessionType.Document,
  SessionType.Exploration,
  SessionType.Comparison
]

const sessionTypeOptions = sessionTypes.map(type => ({
  value: type,
  label: type.replace(/([A-Z])/g, ' $1').trim()
}))
</script>

<template>
  <div class="relative h-full">
    <!-- Pull to refresh indicator -->
    <PullToRefreshIndicator
      v-if="isMobile"
      :pull-distance="pullToRefresh.pullDistance"
      :progress="pullToRefresh.progress"
      :is-refreshing="pullToRefresh.isRefreshing"
    />

    <div
      ref="sessionsListRef"
      class="h-full overflow-y-auto p-4 sm:p-6"
    >
      <!-- Header -->
      <div class="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 class="text-xl sm:text-2xl font-bold text-gray-900 dark:text-white">Sessions</h1>
          <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
            Manage your conversation history
          </p>
        </div>

        <!-- Desktop buttons - hidden on mobile -->
        <div class="hidden sm:flex items-center gap-2">
          <button
            class="btn-secondary gap-2"
            :class="{ 'ring-2 ring-aegis-500': bulkSelection.isSelectionMode.value }"
            @click="bulkSelection.toggleSelectionMode()"
          >
            <CheckIcon class="h-5 w-5" />
            {{ bulkSelection.isSelectionMode.value ? 'Cancel' : 'Select' }}
          </button>
          <button
            class="btn-primary gap-2"
            @click="showNewSessionDialog = true"
          >
            <PlusIcon class="h-5 w-5" />
            New Session
          </button>
        </div>
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

    <template v-else>
      <!-- Bulk Actions Toolbar -->
      <BulkActionsToolbar
        v-if="bulkSelection.isSelectionMode.value"
        :selected-count="bulkSelection.selectedCount.value"
        :all-selected="bulkSelection.allSelected.value"
        :some-selected="bulkSelection.someSelected.value"
        :actions="sessionBulkActions"
        :is-processing="bulkSelection.isProcessing.value"
        item-label="session"
        @toggle-all="bulkSelection.toggleAll()"
        @clear-selection="bulkSelection.clearSelection()"
        @action="bulkSelection.executeAction($event)"
      />

      <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      <div
        v-for="session in filteredSessions"
        :key="session.id"
        class="card group relative p-4 transition-shadow hover:shadow-md"
        :class="{ 'ring-2 ring-aegis-500': bulkSelection.isSelected(session.id) }"
        @click="bulkSelection.isSelectionMode.value ? bulkSelection.toggleItem(session.id) : null"
      >
        <!-- Selection checkbox -->
        <div
          v-if="bulkSelection.isSelectionMode.value"
          class="absolute left-3 top-3 z-10"
          @click.stop="bulkSelection.toggleItem(session.id)"
        >
          <input
            type="checkbox"
            :checked="bulkSelection.isSelected(session.id)"
            class="h-5 w-5 rounded border-gray-300 text-aegis-600 focus:ring-aegis-500 cursor-pointer"
            @change="bulkSelection.toggleItem(session.id)"
          />
        </div>

        <RouterLink
          :to="bulkSelection.isSelectionMode.value ? '' : `/chat/${session.id}`"
          class="block"
          :class="{ 'pl-8': bulkSelection.isSelectionMode.value, 'pointer-events-none': bulkSelection.isSelectionMode.value }"
        >
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

        <!-- Actions (visible on hover, hidden in selection mode) -->
        <div
          v-if="!bulkSelection.isSelectionMode.value"
          class="absolute right-2 top-2 flex gap-1 opacity-0 transition-opacity group-hover:opacity-100"
        >
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
    </template>

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

                <form @submit.prevent="createSession" class="mt-4 space-y-4" novalidate>
                  <FormField
                    v-model="title"
                    name="title"
                    label="Session Title"
                    type="text"
                    placeholder="Enter a title for your session"
                    :error="errors.title"
                    :touched="titleTouched"
                    :required="true"
                    hint="Give your session a descriptive name"
                    @blur="titleTouched = true"
                  />

                  <FormField
                    v-model="sessionType"
                    name="type"
                    label="Session Type"
                    type="select"
                    :options="sessionTypeOptions"
                    :error="errors.type"
                    :touched="true"
                    hint="Choose a template that matches your use case"
                  />

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
                      :disabled="!meta.valid"
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

    <!-- Mobile FAB for new session -->
    <FloatingActionButton
      :icon="PlusIcon"
      @click="showNewSessionDialog = true"
    />
  </div>
</template>
