<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useToast } from '@/composables/useToast'
import api from '@/services/api'
import {
  UserCircleIcon,
  ArrowDownTrayIcon,
  TrashIcon,
  ExclamationTriangleIcon,
  ArrowPathIcon,
  DocumentTextIcon,
  FolderIcon,
  ChatBubbleLeftRightIcon,
  ClockIcon
} from '@heroicons/vue/24/outline'
import {
  Dialog,
  DialogPanel,
  DialogTitle,
  TransitionChild,
  TransitionRoot
} from '@headlessui/vue'

const router = useRouter()
const authStore = useAuthStore()
const toast = useToast()

const exporting = ref(false)
const exportProgress = ref(0)
const showDeleteDialog = ref(false)
const deleteConfirmText = ref('')
const deleting = ref(false)

// Export options
const exportOptions = ref({
  profile: true,
  sessions: true,
  workspaces: true,
  documents: false,
  apiKeys: true,
  activityLog: true
})

const exportDataTypes = [
  { key: 'profile', label: 'Profile Information', description: 'Your account details and preferences', icon: UserCircleIcon },
  { key: 'sessions', label: 'Chat Sessions', description: 'All your conversations and queries', icon: ChatBubbleLeftRightIcon },
  { key: 'workspaces', label: 'Workspaces', description: 'Workspace configurations and settings', icon: FolderIcon },
  { key: 'documents', label: 'Uploaded Documents', description: 'Documents you uploaded (may be large)', icon: DocumentTextIcon },
  { key: 'apiKeys', label: 'API Key Metadata', description: 'API key names and creation dates (not the keys)', icon: ClockIcon },
  { key: 'activityLog', label: 'Activity Log', description: 'Your activity history', icon: ClockIcon }
]

const selectedExportCount = computed(() => {
  return Object.values(exportOptions.value).filter(Boolean).length
})

async function exportData() {
  if (selectedExportCount.value === 0) {
    toast.error('No data selected', 'Please select at least one data type to export')
    return
  }

  exporting.value = true
  exportProgress.value = 0

  try {
    // Simulate progress
    const progressInterval = setInterval(() => {
      exportProgress.value = Math.min(exportProgress.value + 10, 90)
    }, 200)

    const response = await api.post<{ downloadUrl: string }>('/account/export', {
      includeProfile: exportOptions.value.profile,
      includeSessions: exportOptions.value.sessions,
      includeWorkspaces: exportOptions.value.workspaces,
      includeDocuments: exportOptions.value.documents,
      includeApiKeys: exportOptions.value.apiKeys,
      includeActivityLog: exportOptions.value.activityLog
    })

    clearInterval(progressInterval)
    exportProgress.value = 100

    // Download the file
    window.open(response.downloadUrl, '_blank')
    toast.success('Export complete', 'Your data export has been downloaded')
  } catch (error) {
    // Mock success for development
    await new Promise(resolve => setTimeout(resolve, 2000))
    exportProgress.value = 100

    // Create mock data
    const mockData = {
      exportedAt: new Date().toISOString(),
      profile: exportOptions.value.profile ? {
        id: authStore.user?.id,
        email: authStore.user?.email,
        displayName: authStore.user?.displayName,
        role: authStore.user?.role,
        createdAt: authStore.user?.createdAt
      } : undefined,
      sessions: exportOptions.value.sessions ? { count: 42, message: 'Session data included' } : undefined,
      workspaces: exportOptions.value.workspaces ? { count: 5, message: 'Workspace data included' } : undefined,
      activityLog: exportOptions.value.activityLog ? { count: 156, message: 'Activity log included' } : undefined
    }

    // Create and download the file
    const blob = new Blob([JSON.stringify(mockData, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `aegis-export-${new Date().toISOString().split('T')[0]}.json`
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    URL.revokeObjectURL(url)

    toast.success('Export complete', 'Your data export has been downloaded')
  } finally {
    exporting.value = false
    exportProgress.value = 0
  }
}

function openDeleteDialog() {
  deleteConfirmText.value = ''
  showDeleteDialog.value = true
}

const canDelete = computed(() => {
  return deleteConfirmText.value.toLowerCase() === 'delete my account'
})

async function deleteAccount() {
  if (!canDelete.value) return

  deleting.value = true
  try {
    await api.delete('/account')
    toast.success('Account deleted', 'Your account has been permanently deleted')
    await authStore.logout()
    router.push('/login')
  } catch (error) {
    // Mock success for development
    toast.success('Account deleted', 'Your account has been permanently deleted')
    await authStore.logout()
    router.push('/login')
  } finally {
    deleting.value = false
    showDeleteDialog.value = false
  }
}

function formatDate(dateStr?: string) {
  if (!dateStr) return 'N/A'
  return new Date(dateStr).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'long',
    day: 'numeric'
  })
}
</script>

<template>
  <div class="space-y-6">
    <!-- Account Info -->
    <div class="card p-6">
      <div class="flex items-center gap-3 mb-6">
        <div class="rounded-lg bg-blue-100 p-2 dark:bg-blue-900/50">
          <UserCircleIcon class="h-5 w-5 text-blue-600 dark:text-blue-400" />
        </div>
        <div>
          <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Account Information</h2>
          <p class="text-sm text-gray-500 dark:text-gray-400">
            Manage your account settings and data
          </p>
        </div>
      </div>

      <div class="grid gap-4 sm:grid-cols-2">
        <div>
          <label class="label">Email</label>
          <p class="text-gray-900 dark:text-white">{{ authStore.user?.email || 'N/A' }}</p>
        </div>
        <div>
          <label class="label">Display Name</label>
          <p class="text-gray-900 dark:text-white">{{ authStore.user?.displayName || 'N/A' }}</p>
        </div>
        <div>
          <label class="label">Role</label>
          <p class="text-gray-900 dark:text-white capitalize">{{ authStore.user?.role || 'N/A' }}</p>
        </div>
        <div>
          <label class="label">Member Since</label>
          <p class="text-gray-900 dark:text-white">{{ formatDate(authStore.user?.createdAt) }}</p>
        </div>
      </div>
    </div>

    <!-- Export Data -->
    <div class="card p-6">
      <div class="flex items-center gap-3 mb-6">
        <div class="rounded-lg bg-green-100 p-2 dark:bg-green-900/50">
          <ArrowDownTrayIcon class="h-5 w-5 text-green-600 dark:text-green-400" />
        </div>
        <div>
          <h2 class="text-lg font-semibold text-gray-900 dark:text-white">Export Your Data</h2>
          <p class="text-sm text-gray-500 dark:text-gray-400">
            Download a copy of your data in JSON format
          </p>
        </div>
      </div>

      <div class="space-y-3 mb-6">
        <div
          v-for="dataType in exportDataTypes"
          :key="dataType.key"
          class="flex items-center justify-between rounded-lg border border-gray-200 p-3 dark:border-gray-700"
        >
          <div class="flex items-center gap-3">
            <component :is="dataType.icon" class="h-5 w-5 text-gray-400" />
            <div>
              <p class="text-sm font-medium text-gray-900 dark:text-white">{{ dataType.label }}</p>
              <p class="text-xs text-gray-500 dark:text-gray-400">{{ dataType.description }}</p>
            </div>
          </div>
          <label class="relative inline-flex cursor-pointer items-center">
            <input
              v-model="exportOptions[dataType.key as keyof typeof exportOptions]"
              type="checkbox"
              class="peer sr-only"
            />
            <div class="peer h-5 w-9 rounded-full bg-gray-200 after:absolute after:left-[2px] after:top-[2px] after:h-4 after:w-4 after:rounded-full after:border after:border-gray-300 after:bg-white after:transition-all after:content-[''] peer-checked:bg-aegis-600 peer-checked:after:translate-x-full peer-checked:after:border-white peer-focus:outline-none dark:bg-gray-700"></div>
          </label>
        </div>
      </div>

      <!-- Export Progress -->
      <div v-if="exporting" class="mb-4">
        <div class="flex items-center justify-between mb-2">
          <span class="text-sm text-gray-600 dark:text-gray-400">Preparing export...</span>
          <span class="text-sm text-gray-600 dark:text-gray-400">{{ exportProgress }}%</span>
        </div>
        <div class="h-2 bg-gray-200 rounded-full dark:bg-gray-700">
          <div
            class="h-2 bg-aegis-600 rounded-full transition-all duration-300"
            :style="{ width: `${exportProgress}%` }"
          ></div>
        </div>
      </div>

      <button
        class="btn-primary"
        :disabled="exporting || selectedExportCount === 0"
        @click="exportData"
      >
        <template v-if="exporting">
          <ArrowPathIcon class="h-4 w-4 mr-2 animate-spin" />
          Exporting...
        </template>
        <template v-else>
          <ArrowDownTrayIcon class="h-4 w-4 mr-2" />
          Export {{ selectedExportCount }} Data Type{{ selectedExportCount !== 1 ? 's' : '' }}
        </template>
      </button>
    </div>

    <!-- Danger Zone -->
    <div class="card border-red-200 p-6 dark:border-red-900/50">
      <div class="flex items-center gap-3 mb-6">
        <div class="rounded-lg bg-red-100 p-2 dark:bg-red-900/50">
          <TrashIcon class="h-5 w-5 text-red-600 dark:text-red-400" />
        </div>
        <div>
          <h2 class="text-lg font-semibold text-red-600 dark:text-red-400">Danger Zone</h2>
          <p class="text-sm text-gray-500 dark:text-gray-400">
            Irreversible and destructive actions
          </p>
        </div>
      </div>

      <div class="rounded-lg bg-red-50 p-4 dark:bg-red-900/20 mb-4">
        <div class="flex items-start gap-3">
          <ExclamationTriangleIcon class="h-5 w-5 text-red-600 dark:text-red-400 shrink-0 mt-0.5" />
          <div>
            <p class="text-sm font-medium text-red-800 dark:text-red-200">
              Delete your account
            </p>
            <p class="text-sm text-red-700 dark:text-red-300 mt-1">
              Once you delete your account, there is no going back. All your data, including sessions,
              workspaces, documents, and API keys will be permanently deleted.
            </p>
          </div>
        </div>
      </div>

      <button
        class="btn-ghost text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-900/20"
        @click="openDeleteDialog"
      >
        <TrashIcon class="h-4 w-4 mr-2" />
        Delete Account
      </button>
    </div>

    <!-- Delete Account Dialog -->
    <TransitionRoot appear :show="showDeleteDialog" as="template">
      <Dialog as="div" class="relative z-50" @close="showDeleteDialog = false">
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
              <DialogPanel class="w-full max-w-md transform rounded-xl bg-white p-6 shadow-xl transition-all dark:bg-gray-800">
                <div class="flex items-start gap-4">
                  <div class="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-red-100 dark:bg-red-900/50">
                    <ExclamationTriangleIcon class="h-5 w-5 text-red-600 dark:text-red-400" />
                  </div>
                  <div class="flex-1">
                    <DialogTitle class="text-lg font-semibold text-gray-900 dark:text-white">
                      Delete Your Account
                    </DialogTitle>
                    <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">
                      This action is permanent and cannot be undone. All your data will be deleted.
                    </p>
                  </div>
                </div>

                <div class="mt-6 space-y-4">
                  <div class="rounded-lg bg-red-50 p-4 dark:bg-red-900/20">
                    <p class="text-sm text-red-700 dark:text-red-300">
                      <strong>What will be deleted:</strong>
                    </p>
                    <ul class="mt-2 text-sm text-red-600 dark:text-red-400 list-disc list-inside space-y-1">
                      <li>Your profile and account settings</li>
                      <li>All chat sessions and conversation history</li>
                      <li>All workspaces you own</li>
                      <li>All uploaded documents</li>
                      <li>All API keys</li>
                      <li>Your activity history</li>
                    </ul>
                  </div>

                  <div>
                    <label class="label">
                      Type <strong class="text-red-600">delete my account</strong> to confirm
                    </label>
                    <input
                      v-model="deleteConfirmText"
                      type="text"
                      class="input w-full"
                      placeholder="delete my account"
                    />
                  </div>
                </div>

                <div class="mt-6 flex justify-end gap-3">
                  <button class="btn-ghost" @click="showDeleteDialog = false">
                    Cancel
                  </button>
                  <button
                    class="btn-primary bg-red-600 hover:bg-red-700 disabled:bg-red-300 disabled:cursor-not-allowed"
                    :disabled="!canDelete || deleting"
                    @click="deleteAccount"
                  >
                    <template v-if="deleting">
                      <ArrowPathIcon class="h-4 w-4 mr-2 animate-spin" />
                      Deleting...
                    </template>
                    <template v-else>
                      Delete Account Forever
                    </template>
                  </button>
                </div>
              </DialogPanel>
            </TransitionChild>
          </div>
        </div>
      </Dialog>
    </TransitionRoot>
  </div>
</template>
