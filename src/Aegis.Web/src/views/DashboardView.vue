<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useSessionStore } from '@/stores/session'
import { useAuthStore } from '@/stores/auth'
import {
  ChatBubbleLeftRightIcon,
  ClockIcon,
  SparklesIcon,
  ArrowTrendingUpIcon,
  PlusIcon
} from '@heroicons/vue/24/outline'

const router = useRouter()
const sessionStore = useSessionStore()
const authStore = useAuthStore()

onMounted(() => {
  if (authStore.user) {
    sessionStore.fetchStats(authStore.user.id)
    sessionStore.fetchSessions({ userId: authStore.user.id, pageSize: 5 })
  }
})

async function startNewChat() {
  if (!authStore.user) return

  const session = await sessionStore.createSession({
    userId: authStore.user.id,
    title: 'New Conversation',
    type: 'QuickQuery'
  })

  if (session) {
    router.push(`/chat/${session.id}`)
  }
}

function formatDate(dateStr: string | undefined) {
  if (!dateStr) return 'N/A'
  return new Date(dateStr).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}
</script>

<template>
  <div class="p-6">
    <!-- Welcome section -->
    <div class="mb-8">
      <h1 class="text-2xl font-bold text-gray-900 dark:text-white">
        Welcome back, {{ authStore.userName }}
      </h1>
      <p class="mt-1 text-gray-500 dark:text-gray-400">
        Here's what's happening with your RAG workspace.
      </p>
    </div>

    <!-- Stats grid -->
    <div class="mb-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
      <!-- Total Sessions -->
      <div class="card p-5">
        <div class="flex items-center gap-4">
          <div class="rounded-lg bg-aegis-100 p-3 dark:bg-aegis-900/50">
            <ChatBubbleLeftRightIcon class="h-6 w-6 text-aegis-600 dark:text-aegis-400" />
          </div>
          <div>
            <p class="text-sm text-gray-500 dark:text-gray-400">Total Sessions</p>
            <p class="text-2xl font-semibold text-gray-900 dark:text-white">
              {{ sessionStore.stats?.totalSessions ?? 0 }}
            </p>
          </div>
        </div>
      </div>

      <!-- Active Sessions -->
      <div class="card p-5">
        <div class="flex items-center gap-4">
          <div class="rounded-lg bg-green-100 p-3 dark:bg-green-900/50">
            <SparklesIcon class="h-6 w-6 text-green-600 dark:text-green-400" />
          </div>
          <div>
            <p class="text-sm text-gray-500 dark:text-gray-400">Active Sessions</p>
            <p class="text-2xl font-semibold text-gray-900 dark:text-white">
              {{ sessionStore.stats?.activeSessions ?? 0 }}
            </p>
          </div>
        </div>
      </div>

      <!-- Total Queries -->
      <div class="card p-5">
        <div class="flex items-center gap-4">
          <div class="rounded-lg bg-blue-100 p-3 dark:bg-blue-900/50">
            <ArrowTrendingUpIcon class="h-6 w-6 text-blue-600 dark:text-blue-400" />
          </div>
          <div>
            <p class="text-sm text-gray-500 dark:text-gray-400">Queries This Week</p>
            <p class="text-2xl font-semibold text-gray-900 dark:text-white">
              {{ sessionStore.stats?.totalQueriesThisWeek ?? 0 }}
            </p>
          </div>
        </div>
      </div>

      <!-- Avg Duration -->
      <div class="card p-5">
        <div class="flex items-center gap-4">
          <div class="rounded-lg bg-amber-100 p-3 dark:bg-amber-900/50">
            <ClockIcon class="h-6 w-6 text-amber-600 dark:text-amber-400" />
          </div>
          <div>
            <p class="text-sm text-gray-500 dark:text-gray-400">Avg Duration</p>
            <p class="text-2xl font-semibold text-gray-900 dark:text-white">
              {{ Math.round(sessionStore.stats?.averageSessionDuration ?? 0) }}m
            </p>
          </div>
        </div>
      </div>
    </div>

    <!-- Quick actions and recent sessions -->
    <div class="grid gap-6 lg:grid-cols-2">
      <!-- Quick Actions -->
      <div class="card p-6">
        <h2 class="mb-4 text-lg font-semibold text-gray-900 dark:text-white">
          Quick Actions
        </h2>
        <div class="space-y-3">
          <button
            class="flex w-full items-center gap-3 rounded-lg border-2 border-dashed border-gray-300 p-4 text-left transition-colors hover:border-aegis-400 hover:bg-aegis-50 dark:border-gray-600 dark:hover:border-aegis-500 dark:hover:bg-aegis-900/20"
            @click="startNewChat"
          >
            <div class="rounded-lg bg-aegis-100 p-2 dark:bg-aegis-900/50">
              <PlusIcon class="h-5 w-5 text-aegis-600 dark:text-aegis-400" />
            </div>
            <div>
              <p class="font-medium text-gray-900 dark:text-white">Start New Chat</p>
              <p class="text-sm text-gray-500 dark:text-gray-400">
                Begin a new conversation with the RAG system
              </p>
            </div>
          </button>

          <RouterLink
            to="/sessions"
            class="flex w-full items-center gap-3 rounded-lg border-2 border-dashed border-gray-300 p-4 text-left transition-colors hover:border-gray-400 hover:bg-gray-50 dark:border-gray-600 dark:hover:border-gray-500 dark:hover:bg-gray-700/20"
          >
            <div class="rounded-lg bg-gray-100 p-2 dark:bg-gray-700">
              <ClockIcon class="h-5 w-5 text-gray-600 dark:text-gray-400" />
            </div>
            <div>
              <p class="font-medium text-gray-900 dark:text-white">View All Sessions</p>
              <p class="text-sm text-gray-500 dark:text-gray-400">
                Browse and manage your conversation history
              </p>
            </div>
          </RouterLink>
        </div>
      </div>

      <!-- Recent Sessions -->
      <div class="card p-6">
        <div class="mb-4 flex items-center justify-between">
          <h2 class="text-lg font-semibold text-gray-900 dark:text-white">
            Recent Sessions
          </h2>
          <RouterLink
            to="/sessions"
            class="text-sm font-medium text-aegis-600 hover:text-aegis-700 dark:text-aegis-400"
          >
            View all
          </RouterLink>
        </div>

        <div v-if="sessionStore.sessions.length === 0" class="py-8 text-center text-gray-500 dark:text-gray-400">
          No sessions yet. Start a new chat to begin!
        </div>

        <ul v-else class="divide-y divide-gray-200 dark:divide-gray-700">
          <li
            v-for="session in sessionStore.sessions.slice(0, 5)"
            :key="session.id"
          >
            <RouterLink
              :to="`/chat/${session.id}`"
              class="block py-3 transition-colors hover:bg-gray-50 dark:hover:bg-gray-700/50"
            >
              <div class="flex items-center justify-between">
                <div class="min-w-0 flex-1">
                  <p class="truncate font-medium text-gray-900 dark:text-white">
                    {{ session.title }}
                  </p>
                  <p class="text-sm text-gray-500 dark:text-gray-400">
                    {{ session.turnCount }} turns · {{ formatDate(session.lastActivityAt) }}
                  </p>
                </div>
                <span
                  class="ml-2 inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium"
                  :class="session.status === 'Active'
                    ? 'bg-green-100 text-green-700 dark:bg-green-900/50 dark:text-green-300'
                    : 'bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-400'"
                >
                  {{ session.status }}
                </span>
              </div>
            </RouterLink>
          </li>
        </ul>
      </div>
    </div>
  </div>
</template>
