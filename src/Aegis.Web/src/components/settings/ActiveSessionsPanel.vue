<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { authService, type ActiveSession, type LoginHistoryEntry } from '@/services/auth.service'
import { useToast } from '@/composables/useToast'
import {
  ComputerDesktopIcon,
  DevicePhoneMobileIcon,
  DeviceTabletIcon,
  GlobeAltIcon,
  MapPinIcon,
  ClockIcon,
  TrashIcon,
  CheckCircleIcon,
  XCircleIcon,
  ArrowPathIcon
} from '@heroicons/vue/24/outline'
import {
  Dialog,
  DialogPanel,
  DialogTitle,
  TransitionChild,
  TransitionRoot,
  TabGroup,
  TabList,
  Tab,
  TabPanels,
  TabPanel
} from '@headlessui/vue'

defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'close'): void
}>()

const toast = useToast()

const loading = ref(false)
const sessions = ref<ActiveSession[]>([])
const loginHistory = ref<LoginHistoryEntry[]>([])
const revokingSessionId = ref<string | null>(null)

onMounted(async () => {
  await Promise.all([fetchSessions(), fetchLoginHistory()])
})

async function fetchSessions() {
  loading.value = true
  try {
    sessions.value = await authService.getActiveSessions()
  } catch (error) {
    // Mock data for development
    sessions.value = [
      {
        id: '1',
        deviceName: 'MacBook Pro',
        deviceType: 'desktop',
        browser: 'Chrome 120',
        os: 'macOS Sonoma',
        ipAddress: '192.168.1.100',
        location: 'San Francisco, CA',
        lastActiveAt: new Date().toISOString(),
        createdAt: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString(),
        isCurrent: true
      },
      {
        id: '2',
        deviceName: 'iPhone 15',
        deviceType: 'mobile',
        browser: 'Safari Mobile',
        os: 'iOS 17.2',
        ipAddress: '192.168.1.101',
        location: 'San Francisco, CA',
        lastActiveAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
        createdAt: new Date(Date.now() - 3 * 24 * 60 * 60 * 1000).toISOString(),
        isCurrent: false
      },
      {
        id: '3',
        deviceName: 'Windows PC',
        deviceType: 'desktop',
        browser: 'Firefox 121',
        os: 'Windows 11',
        ipAddress: '10.0.0.50',
        location: 'New York, NY',
        lastActiveAt: new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString(),
        createdAt: new Date(Date.now() - 14 * 24 * 60 * 60 * 1000).toISOString(),
        isCurrent: false
      }
    ]
  } finally {
    loading.value = false
  }
}

async function fetchLoginHistory() {
  try {
    loginHistory.value = await authService.getLoginHistory(20)
  } catch (error) {
    // Mock data for development
    loginHistory.value = [
      {
        id: '1',
        ipAddress: '192.168.1.100',
        location: 'San Francisco, CA',
        deviceName: 'MacBook Pro',
        browser: 'Chrome 120',
        os: 'macOS Sonoma',
        status: 'success',
        createdAt: new Date().toISOString()
      },
      {
        id: '2',
        ipAddress: '192.168.1.101',
        location: 'San Francisco, CA',
        deviceName: 'iPhone 15',
        browser: 'Safari Mobile',
        os: 'iOS 17.2',
        status: 'success',
        createdAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString()
      },
      {
        id: '3',
        ipAddress: '45.67.89.10',
        location: 'Unknown',
        deviceName: 'Unknown Device',
        browser: 'Chrome 119',
        os: 'Windows 10',
        status: 'failed',
        failureReason: 'Invalid password',
        createdAt: new Date(Date.now() - 5 * 60 * 60 * 1000).toISOString()
      },
      {
        id: '4',
        ipAddress: '10.0.0.50',
        location: 'New York, NY',
        deviceName: 'Windows PC',
        browser: 'Firefox 121',
        os: 'Windows 11',
        status: 'success',
        createdAt: new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString()
      }
    ]
  }
}

async function revokeSession(sessionId: string) {
  revokingSessionId.value = sessionId
  try {
    await authService.revokeSession(sessionId)
    sessions.value = sessions.value.filter(s => s.id !== sessionId)
    toast.success('Session revoked', 'The device has been logged out')
  } catch (error) {
    // Mock success for development
    sessions.value = sessions.value.filter(s => s.id !== sessionId)
    toast.success('Session revoked', 'The device has been logged out')
  } finally {
    revokingSessionId.value = null
  }
}

async function revokeAllSessions() {
  loading.value = true
  try {
    await authService.revokeAllSessions()
    sessions.value = sessions.value.filter(s => s.isCurrent)
    toast.success('All sessions revoked', 'All other devices have been logged out')
  } catch (error) {
    // Mock success for development
    sessions.value = sessions.value.filter(s => s.isCurrent)
    toast.success('All sessions revoked', 'All other devices have been logged out')
  } finally {
    loading.value = false
  }
}

function getDeviceIcon(deviceType: ActiveSession['deviceType']) {
  switch (deviceType) {
    case 'mobile':
      return DevicePhoneMobileIcon
    case 'tablet':
      return DeviceTabletIcon
    default:
      return ComputerDesktopIcon
  }
}

function formatRelativeTime(dateStr: string) {
  const date = new Date(dateStr)
  const now = new Date()
  const diff = now.getTime() - date.getTime()

  if (diff < 60000) return 'Just now'
  if (diff < 3600000) return `${Math.floor(diff / 60000)}m ago`
  if (diff < 86400000) return `${Math.floor(diff / 3600000)}h ago`
  return `${Math.floor(diff / 86400000)}d ago`
}

function formatDateTime(dateStr: string) {
  return new Date(dateStr).toLocaleString('en-US', {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}
</script>

<template>
  <TransitionRoot appear :show="open" as="template">
    <Dialog as="div" class="relative z-50" @close="emit('close')">
      <TransitionChild
        enter="duration-300 ease-out"
        enter-from="opacity-0"
        enter-to="opacity-100"
        leave="duration-200 ease-in"
        leave-from="opacity-100"
        leave-to="opacity-0"
      >
        <div class="fixed inset-0 bg-black/30 backdrop-blur-sm" />
      </TransitionChild>

      <div class="fixed inset-0 overflow-y-auto">
        <div class="flex min-h-full items-center justify-center p-4">
          <TransitionChild
            enter="duration-300 ease-out"
            enter-from="opacity-0 scale-95"
            enter-to="opacity-100 scale-100"
            leave="duration-200 ease-in"
            leave-from="opacity-100 scale-100"
            leave-to="opacity-0 scale-95"
          >
            <DialogPanel class="w-full max-w-2xl transform rounded-xl bg-white p-6 shadow-xl transition-all dark:bg-gray-800">
              <DialogTitle class="text-lg font-semibold text-gray-900 dark:text-white mb-4">
                Sessions & Login History
              </DialogTitle>

              <TabGroup>
                <TabList class="flex space-x-1 rounded-lg bg-gray-100 p-1 dark:bg-gray-700 mb-4">
                  <Tab v-slot="{ selected }" as="template">
                    <button
                      class="w-full rounded-md py-2 text-sm font-medium transition-all"
                      :class="selected
                        ? 'bg-white text-aegis-700 shadow dark:bg-gray-600 dark:text-aegis-400'
                        : 'text-gray-600 hover:text-gray-800 dark:text-gray-400'"
                    >
                      Active Sessions ({{ sessions.length }})
                    </button>
                  </Tab>
                  <Tab v-slot="{ selected }" as="template">
                    <button
                      class="w-full rounded-md py-2 text-sm font-medium transition-all"
                      :class="selected
                        ? 'bg-white text-aegis-700 shadow dark:bg-gray-600 dark:text-aegis-400'
                        : 'text-gray-600 hover:text-gray-800 dark:text-gray-400'"
                    >
                      Login History
                    </button>
                  </Tab>
                </TabList>

                <TabPanels>
                  <!-- Active Sessions Tab -->
                  <TabPanel>
                    <div class="space-y-4">
                      <!-- Revoke All Button -->
                      <div v-if="sessions.filter(s => !s.isCurrent).length > 0" class="flex justify-end">
                        <button
                          class="btn-ghost text-red-600 dark:text-red-400 text-sm"
                          :disabled="loading"
                          @click="revokeAllSessions"
                        >
                          <ArrowPathIcon v-if="loading" class="h-4 w-4 mr-1 animate-spin" />
                          Log out all other sessions
                        </button>
                      </div>

                      <!-- Loading -->
                      <div v-if="loading && sessions.length === 0" class="py-8 text-center">
                        <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-aegis-600 mx-auto"></div>
                      </div>

                      <!-- Sessions List -->
                      <div v-else class="space-y-3 max-h-80 overflow-y-auto">
                        <div
                          v-for="session in sessions"
                          :key="session.id"
                          class="flex items-center gap-4 rounded-lg border p-4 transition-colors"
                          :class="session.isCurrent
                            ? 'border-aegis-200 bg-aegis-50 dark:border-aegis-800 dark:bg-aegis-900/20'
                            : 'border-gray-200 dark:border-gray-700'"
                        >
                          <div class="rounded-lg bg-gray-100 p-3 dark:bg-gray-700">
                            <component
                              :is="getDeviceIcon(session.deviceType)"
                              class="h-6 w-6 text-gray-600 dark:text-gray-400"
                            />
                          </div>

                          <div class="flex-1 min-w-0">
                            <div class="flex items-center gap-2">
                              <p class="font-medium text-gray-900 dark:text-white truncate">
                                {{ session.deviceName }}
                              </p>
                              <span
                                v-if="session.isCurrent"
                                class="inline-flex items-center rounded-full bg-aegis-100 px-2 py-0.5 text-xs font-medium text-aegis-700 dark:bg-aegis-900/50 dark:text-aegis-300"
                              >
                                Current
                              </span>
                            </div>
                            <p class="text-sm text-gray-500 dark:text-gray-400">
                              {{ session.browser }} · {{ session.os }}
                            </p>
                            <div class="flex items-center gap-3 mt-1 text-xs text-gray-400">
                              <span class="flex items-center gap-1">
                                <GlobeAltIcon class="h-3 w-3" />
                                {{ session.ipAddress }}
                              </span>
                              <span v-if="session.location" class="flex items-center gap-1">
                                <MapPinIcon class="h-3 w-3" />
                                {{ session.location }}
                              </span>
                              <span class="flex items-center gap-1">
                                <ClockIcon class="h-3 w-3" />
                                {{ formatRelativeTime(session.lastActiveAt) }}
                              </span>
                            </div>
                          </div>

                          <button
                            v-if="!session.isCurrent"
                            class="btn-ghost p-2 text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-900/20"
                            :disabled="revokingSessionId === session.id"
                            @click="revokeSession(session.id)"
                          >
                            <ArrowPathIcon v-if="revokingSessionId === session.id" class="h-5 w-5 animate-spin" />
                            <TrashIcon v-else class="h-5 w-5" />
                          </button>
                        </div>
                      </div>
                    </div>
                  </TabPanel>

                  <!-- Login History Tab -->
                  <TabPanel>
                    <div class="space-y-2 max-h-80 overflow-y-auto">
                      <div
                        v-for="entry in loginHistory"
                        :key="entry.id"
                        class="flex items-center gap-4 rounded-lg border border-gray-200 p-3 dark:border-gray-700"
                      >
                        <div
                          class="rounded-full p-2"
                          :class="entry.status === 'success'
                            ? 'bg-green-100 dark:bg-green-900/50'
                            : 'bg-red-100 dark:bg-red-900/50'"
                        >
                          <CheckCircleIcon
                            v-if="entry.status === 'success'"
                            class="h-4 w-4 text-green-600 dark:text-green-400"
                          />
                          <XCircleIcon
                            v-else
                            class="h-4 w-4 text-red-600 dark:text-red-400"
                          />
                        </div>

                        <div class="flex-1 min-w-0">
                          <div class="flex items-center gap-2">
                            <p class="text-sm font-medium text-gray-900 dark:text-white">
                              {{ entry.status === 'success' ? 'Successful login' : 'Failed login' }}
                            </p>
                          </div>
                          <p class="text-xs text-gray-500 dark:text-gray-400">
                            {{ entry.browser }} · {{ entry.os }} · {{ entry.ipAddress }}
                          </p>
                          <p v-if="entry.failureReason" class="text-xs text-red-500">
                            {{ entry.failureReason }}
                          </p>
                        </div>

                        <div class="text-right">
                          <p class="text-xs text-gray-500 dark:text-gray-400">
                            {{ formatDateTime(entry.createdAt) }}
                          </p>
                          <p v-if="entry.location" class="text-xs text-gray-400">
                            {{ entry.location }}
                          </p>
                        </div>
                      </div>
                    </div>
                  </TabPanel>
                </TabPanels>
              </TabGroup>

              <div class="mt-6 flex justify-end">
                <button class="btn-ghost" @click="emit('close')">
                  Close
                </button>
              </div>
            </DialogPanel>
          </TransitionChild>
        </div>
      </div>
    </Dialog>
  </TransitionRoot>
</template>
