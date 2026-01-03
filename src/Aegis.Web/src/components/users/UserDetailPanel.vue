<script setup lang="ts">
import { ref, watch } from 'vue'
import {
  Dialog,
  DialogPanel,
  DialogTitle,
  TransitionRoot,
  TransitionChild,
  TabGroup,
  TabList,
  Tab,
  TabPanels,
  TabPanel
} from '@headlessui/vue'
import {
  XMarkIcon,
  ShieldCheckIcon,
  ClockIcon,
  DevicePhoneMobileIcon,
  ComputerDesktopIcon,
  GlobeAltIcon,
  TrashIcon
} from '@heroicons/vue/24/outline'
import { CheckCircleIcon, XCircleIcon, LockClosedIcon } from '@heroicons/vue/24/solid'
import type { ManagedUser, UserActivityEntry, UserSession, UserAction, ManagedTeam } from '@/types'
import { getRoleColor, getRoleLabel, formatUserStatus } from '@/types/userManagement'
import { useUserManagementStore } from '@/stores/userManagement'

const props = defineProps<{
  isOpen: boolean
  user: ManagedUser | null
  teams?: ManagedTeam[]
}>()

const emit = defineEmits<{
  close: []
  action: [userId: string, action: UserAction]
  edit: [user: ManagedUser]
}>()

const store = useUserManagementStore()
const selectedTab = ref(0)
const activities = ref<UserActivityEntry[]>([])
const sessions = ref<UserSession[]>([])
const isLoadingActivities = ref(false)
const isLoadingSessions = ref(false)

watch(() => props.user, async (user) => {
  if (user) {
    selectedTab.value = 0
    await loadActivities()
    await loadSessions()
  }
}, { immediate: true })

async function loadActivities() {
  if (!props.user) return
  isLoadingActivities.value = true
  try {
    await store.fetchUserActivities(props.user.id)
    activities.value = store.userActivities
  } finally {
    isLoadingActivities.value = false
  }
}

async function loadSessions() {
  if (!props.user) return
  isLoadingSessions.value = true
  try {
    await store.fetchUserSessions(props.user.id)
    sessions.value = store.userSessions
  } finally {
    isLoadingSessions.value = false
  }
}

async function terminateSession(sessionId: string) {
  if (!props.user) return
  await store.terminateUserSession(props.user.id, sessionId)
  sessions.value = sessions.value.filter(s => s.id !== sessionId)
}

function getRoleClasses(color: string): string {
  switch (color) {
    case 'gray':
      return 'bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300'
    case 'blue':
      return 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400'
    case 'green':
      return 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400'
    case 'purple':
      return 'bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-400'
    case 'red':
      return 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400'
    default:
      return 'bg-gray-100 text-gray-800 dark:bg-gray-700 dark:text-gray-300'
  }
}

function formatDate(dateStr: string | undefined): string {
  if (!dateStr) return 'Never'
  return new Date(dateStr).toLocaleString()
}

function formatRelativeTime(dateStr: string): string {
  const date = new Date(dateStr)
  const now = new Date()
  const diffMs = now.getTime() - date.getTime()
  const diffMins = Math.floor(diffMs / (60 * 1000))
  const diffHours = Math.floor(diffMs / (60 * 60 * 1000))
  const diffDays = Math.floor(diffMs / (24 * 60 * 60 * 1000))

  if (diffMins < 1) return 'Just now'
  if (diffMins < 60) return `${diffMins}m ago`
  if (diffHours < 24) return `${diffHours}h ago`
  if (diffDays < 7) return `${diffDays}d ago`
  return date.toLocaleDateString()
}

function getDeviceIcon(type: string) {
  switch (type) {
    case 'mobile':
      return DevicePhoneMobileIcon
    case 'desktop':
      return ComputerDesktopIcon
    default:
      return GlobeAltIcon
  }
}
</script>

<template>
  <TransitionRoot appear :show="isOpen" as="template">
    <Dialog as="div" class="relative z-50" @close="emit('close')">
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
            <DialogPanel
              class="w-full max-w-3xl transform overflow-hidden rounded-xl bg-white shadow-xl transition-all dark:bg-gray-800"
            >
              <!-- Header -->
              <div class="flex items-start justify-between border-b px-6 py-4 dark:border-gray-700">
                <div v-if="user" class="flex items-center gap-4">
                  <div
                    class="flex h-16 w-16 items-center justify-center rounded-full bg-aegis-100 text-2xl font-semibold text-aegis-700 dark:bg-aegis-900/30 dark:text-aegis-400"
                  >
                    {{ user.name.charAt(0).toUpperCase() }}
                  </div>
                  <div>
                    <div class="flex items-center gap-2">
                      <DialogTitle class="text-xl font-semibold text-gray-900 dark:text-white">
                        {{ user.name }}
                      </DialogTitle>
                      <span
                        class="inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium"
                        :class="getRoleClasses(getRoleColor(user.role))"
                      >
                        {{ getRoleLabel(user.role) }}
                      </span>
                    </div>
                    <p class="text-sm text-gray-500 dark:text-gray-400">{{ user.email }}</p>
                    <div class="mt-1 flex items-center gap-3">
                      <!-- Status badges -->
                      <span
                        class="inline-flex items-center gap-1 text-xs"
                        :class="{
                          'text-green-600 dark:text-green-400': user.isActive && !user.isLocked && user.emailVerified,
                          'text-red-600 dark:text-red-400': user.isLocked,
                          'text-yellow-600 dark:text-yellow-400': !user.emailVerified,
                          'text-gray-500 dark:text-gray-400': !user.isActive
                        }"
                      >
                        <CheckCircleIcon v-if="user.isActive && !user.isLocked && user.emailVerified" class="h-4 w-4" />
                        <LockClosedIcon v-else-if="user.isLocked" class="h-4 w-4" />
                        <ClockIcon v-else-if="!user.emailVerified" class="h-4 w-4" />
                        <XCircleIcon v-else class="h-4 w-4" />
                        {{ formatUserStatus(user) }}
                      </span>
                      <span v-if="user.mfaEnabled" class="inline-flex items-center gap-1 text-xs text-green-600 dark:text-green-400">
                        <ShieldCheckIcon class="h-4 w-4" />
                        MFA Enabled
                      </span>
                    </div>
                  </div>
                </div>
                <button
                  type="button"
                  class="rounded-lg p-1.5 text-gray-400 hover:bg-gray-100 hover:text-gray-500 dark:hover:bg-gray-700"
                  @click="emit('close')"
                >
                  <XMarkIcon class="h-5 w-5" />
                </button>
              </div>

              <!-- Content -->
              <div v-if="user" class="max-h-[60vh] overflow-y-auto">
                <TabGroup :selected-index="selectedTab" @change="selectedTab = $event">
                  <TabList class="flex gap-4 border-b px-6 dark:border-gray-700">
                    <Tab v-slot="{ selected }" as="template">
                      <button
                        class="border-b-2 px-1 py-3 text-sm font-medium transition-colors"
                        :class="[
                          selected
                            ? 'border-aegis-500 text-aegis-600 dark:text-aegis-400'
                            : 'border-transparent text-gray-500 hover:border-gray-300 dark:text-gray-400'
                        ]"
                      >
                        Overview
                      </button>
                    </Tab>
                    <Tab v-slot="{ selected }" as="template">
                      <button
                        class="border-b-2 px-1 py-3 text-sm font-medium transition-colors"
                        :class="[
                          selected
                            ? 'border-aegis-500 text-aegis-600 dark:text-aegis-400'
                            : 'border-transparent text-gray-500 hover:border-gray-300 dark:text-gray-400'
                        ]"
                      >
                        Activity
                      </button>
                    </Tab>
                    <Tab v-slot="{ selected }" as="template">
                      <button
                        class="border-b-2 px-1 py-3 text-sm font-medium transition-colors"
                        :class="[
                          selected
                            ? 'border-aegis-500 text-aegis-600 dark:text-aegis-400'
                            : 'border-transparent text-gray-500 hover:border-gray-300 dark:text-gray-400'
                        ]"
                      >
                        Sessions
                      </button>
                    </Tab>
                  </TabList>

                  <TabPanels class="p-6">
                    <!-- Overview Tab -->
                    <TabPanel>
                      <div class="grid grid-cols-2 gap-6">
                        <!-- Account Info -->
                        <div>
                          <h4 class="mb-3 text-sm font-medium text-gray-700 dark:text-gray-300">Account Information</h4>
                          <dl class="space-y-2">
                            <div class="flex justify-between">
                              <dt class="text-sm text-gray-500 dark:text-gray-400">User ID</dt>
                              <dd class="text-sm font-mono text-gray-900 dark:text-white">{{ user.id.slice(0, 8) }}...</dd>
                            </div>
                            <div class="flex justify-between">
                              <dt class="text-sm text-gray-500 dark:text-gray-400">Team</dt>
                              <dd class="text-sm text-gray-900 dark:text-white">{{ user.teamName || 'None' }}</dd>
                            </div>
                            <div class="flex justify-between">
                              <dt class="text-sm text-gray-500 dark:text-gray-400">Created</dt>
                              <dd class="text-sm text-gray-900 dark:text-white">{{ formatDate(user.createdAt) }}</dd>
                            </div>
                            <div class="flex justify-between">
                              <dt class="text-sm text-gray-500 dark:text-gray-400">Last Login</dt>
                              <dd class="text-sm text-gray-900 dark:text-white">{{ formatDate(user.lastLoginAt) }}</dd>
                            </div>
                            <div class="flex justify-between">
                              <dt class="text-sm text-gray-500 dark:text-gray-400">Login Count</dt>
                              <dd class="text-sm text-gray-900 dark:text-white">{{ user.loginCount }}</dd>
                            </div>
                            <div class="flex justify-between">
                              <dt class="text-sm text-gray-500 dark:text-gray-400">Failed Logins</dt>
                              <dd class="text-sm" :class="user.failedLoginAttempts > 3 ? 'text-red-600' : 'text-gray-900 dark:text-white'">
                                {{ user.failedLoginAttempts }}
                              </dd>
                            </div>
                          </dl>
                        </div>

                        <!-- Usage Stats -->
                        <div>
                          <h4 class="mb-3 text-sm font-medium text-gray-700 dark:text-gray-300">Usage Statistics</h4>
                          <div class="grid grid-cols-2 gap-3">
                            <div class="rounded-lg border p-3 dark:border-gray-700">
                              <p class="text-2xl font-bold text-gray-900 dark:text-white">{{ user.queryCount }}</p>
                              <p class="text-xs text-gray-500 dark:text-gray-400">Queries</p>
                            </div>
                            <div class="rounded-lg border p-3 dark:border-gray-700">
                              <p class="text-2xl font-bold text-gray-900 dark:text-white">{{ user.sessionCount }}</p>
                              <p class="text-xs text-gray-500 dark:text-gray-400">Sessions</p>
                            </div>
                            <div class="rounded-lg border p-3 dark:border-gray-700">
                              <p class="text-2xl font-bold text-gray-900 dark:text-white">{{ user.workspaceCount }}</p>
                              <p class="text-xs text-gray-500 dark:text-gray-400">Workspaces</p>
                            </div>
                            <div class="rounded-lg border p-3 dark:border-gray-700">
                              <p class="text-2xl font-bold text-gray-900 dark:text-white">{{ user.documentCount }}</p>
                              <p class="text-xs text-gray-500 dark:text-gray-400">Documents</p>
                            </div>
                          </div>
                        </div>
                      </div>
                    </TabPanel>

                    <!-- Activity Tab -->
                    <TabPanel>
                      <div v-if="isLoadingActivities" class="space-y-3">
                        <div v-for="i in 5" :key="i" class="h-12 animate-pulse rounded bg-gray-200 dark:bg-gray-700" />
                      </div>
                      <div v-else-if="activities.length === 0" class="py-8 text-center text-gray-500 dark:text-gray-400">
                        No activity recorded
                      </div>
                      <div v-else class="space-y-3">
                        <div
                          v-for="activity in activities"
                          :key="activity.id"
                          class="flex items-center justify-between rounded-lg border p-3 dark:border-gray-700"
                        >
                          <div class="flex items-center gap-3">
                            <div
                              class="flex h-8 w-8 items-center justify-center rounded-full"
                              :class="activity.success ? 'bg-green-100 dark:bg-green-900/30' : 'bg-red-100 dark:bg-red-900/30'"
                            >
                              <CheckCircleIcon v-if="activity.success" class="h-4 w-4 text-green-600" />
                              <XCircleIcon v-else class="h-4 w-4 text-red-600" />
                            </div>
                            <div>
                              <p class="text-sm font-medium text-gray-900 dark:text-white">{{ activity.action }}</p>
                              <p class="text-xs text-gray-500 dark:text-gray-400">
                                {{ activity.resourceType }} {{ activity.resourceName ? `- ${activity.resourceName}` : '' }}
                              </p>
                            </div>
                          </div>
                          <div class="text-right">
                            <p class="text-xs text-gray-500 dark:text-gray-400">{{ formatRelativeTime(activity.timestamp) }}</p>
                            <p v-if="activity.ipAddress" class="text-xs text-gray-400">{{ activity.ipAddress }}</p>
                          </div>
                        </div>
                      </div>
                    </TabPanel>

                    <!-- Sessions Tab -->
                    <TabPanel>
                      <div v-if="isLoadingSessions" class="space-y-3">
                        <div v-for="i in 3" :key="i" class="h-16 animate-pulse rounded bg-gray-200 dark:bg-gray-700" />
                      </div>
                      <div v-else-if="sessions.length === 0" class="py-8 text-center text-gray-500 dark:text-gray-400">
                        No active sessions
                      </div>
                      <div v-else class="space-y-3">
                        <div
                          v-for="session in sessions"
                          :key="session.id"
                          class="flex items-center justify-between rounded-lg border p-4 dark:border-gray-700"
                          :class="session.isCurrent ? 'border-aegis-500 bg-aegis-50 dark:bg-aegis-900/10' : ''"
                        >
                          <div class="flex items-center gap-4">
                            <component
                              :is="getDeviceIcon(session.deviceType)"
                              class="h-8 w-8 text-gray-400"
                            />
                            <div>
                              <div class="flex items-center gap-2">
                                <p class="text-sm font-medium text-gray-900 dark:text-white">
                                  {{ session.browser }} on {{ session.os }}
                                </p>
                                <span
                                  v-if="session.isCurrent"
                                  class="rounded-full bg-aegis-100 px-2 py-0.5 text-xs font-medium text-aegis-700 dark:bg-aegis-900/30 dark:text-aegis-400"
                                >
                                  Current
                                </span>
                              </div>
                              <p class="text-xs text-gray-500 dark:text-gray-400">
                                {{ session.ipAddress }}{{ session.location ? ` - ${session.location}` : '' }}
                              </p>
                              <p class="text-xs text-gray-400">
                                Last active {{ formatRelativeTime(session.lastActiveAt) }}
                              </p>
                            </div>
                          </div>
                          <button
                            v-if="!session.isCurrent"
                            class="rounded-lg p-2 text-red-500 hover:bg-red-50 dark:hover:bg-red-900/20"
                            title="Terminate session"
                            @click="terminateSession(session.id)"
                          >
                            <TrashIcon class="h-5 w-5" />
                          </button>
                        </div>
                      </div>
                    </TabPanel>
                  </TabPanels>
                </TabGroup>
              </div>

              <!-- Footer -->
              <div class="flex justify-between border-t px-6 py-4 dark:border-gray-700">
                <div class="flex gap-2">
                  <button
                    v-if="user"
                    class="btn-secondary text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-900/20"
                    @click="emit('action', user.id, 'delete')"
                  >
                    Delete User
                  </button>
                </div>
                <div class="flex gap-2">
                  <button class="btn-secondary" @click="emit('close')">
                    Close
                  </button>
                  <button v-if="user" class="btn-primary" @click="emit('edit', user)">
                    Edit User
                  </button>
                </div>
              </div>
            </DialogPanel>
          </TransitionChild>
        </div>
      </div>
    </Dialog>
  </TransitionRoot>
</template>
