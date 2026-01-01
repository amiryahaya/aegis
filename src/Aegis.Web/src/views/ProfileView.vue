<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { TabGroup, TabList, Tab, TabPanels, TabPanel } from '@headlessui/vue'
import {
  UserCircleIcon,
  ChartBarIcon,
  KeyIcon,
  ShieldCheckIcon,
  PencilIcon,
  CameraIcon,
  TrashIcon,
  PlusIcon,
  ClipboardDocumentIcon,
  CheckIcon,
  ExclamationTriangleIcon
} from '@heroicons/vue/24/outline'
import { useProfileStore } from '@/stores/profile'
import { useAuthStore } from '@/stores/auth'
import type { UpdateProfileRequest, ProfileApiKeyRequest } from '@/types/profile'

const profileStore = useProfileStore()
const authStore = useAuthStore()

const isEditing = ref(false)
const showChangePassword = ref(false)
const showCreateApiKey = ref(false)
const showDeleteConfirm = ref(false)
const copiedKeyId = ref<string | null>(null)
const newApiKey = ref<string | null>(null)

// Edit form
const editForm = ref<UpdateProfileRequest>({
  name: '',
  displayName: '',
  bio: '',
  department: '',
  location: '',
  timezone: '',
  language: ''
})

// Password form
const passwordForm = ref({
  currentPassword: '',
  newPassword: '',
  confirmPassword: ''
})

// API key form
const apiKeyForm = ref<ProfileApiKeyRequest>({
  name: '',
  scopes: ['read'],
  expiresInDays: 90
})

const availableScopes = [
  { value: 'read', label: 'Read', description: 'Read access to resources' },
  { value: 'write', label: 'Write', description: 'Create and update resources' },
  { value: 'delete', label: 'Delete', description: 'Delete resources' },
  { value: 'admin', label: 'Admin', description: 'Administrative operations' }
]

const tabs = [
  { name: 'Profile', icon: UserCircleIcon },
  { name: 'Activity', icon: ChartBarIcon },
  { name: 'API Keys', icon: KeyIcon },
  { name: 'Security', icon: ShieldCheckIcon }
]

const initials = computed(() => {
  const name = profileStore.profile?.name || authStore.userName
  return name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2)
})

onMounted(async () => {
  await Promise.all([
    profileStore.fetchProfile(),
    profileStore.fetchStats(),
    profileStore.fetchApiKeys()
  ])
})

function startEditing() {
  if (profileStore.profile) {
    editForm.value = {
      name: profileStore.profile.name,
      displayName: profileStore.profile.displayName || '',
      bio: profileStore.profile.bio || '',
      department: profileStore.profile.department || '',
      location: profileStore.profile.location || '',
      timezone: profileStore.profile.timezone,
      language: profileStore.profile.language
    }
  }
  isEditing.value = true
}

async function saveProfile() {
  const success = await profileStore.updateProfile(editForm.value)
  if (success) {
    isEditing.value = false
  }
}

function cancelEditing() {
  isEditing.value = false
}

async function handleAvatarUpload(event: Event) {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0]
  if (file) {
    await profileStore.uploadAvatar(file)
  }
}

async function removeAvatar() {
  await profileStore.removeAvatar()
}

async function changePassword() {
  if (passwordForm.value.newPassword !== passwordForm.value.confirmPassword) {
    profileStore.error = 'Passwords do not match'
    return
  }

  const success = await profileStore.changePassword(passwordForm.value)
  if (success) {
    showChangePassword.value = false
    passwordForm.value = { currentPassword: '', newPassword: '', confirmPassword: '' }
  }
}

async function createApiKey() {
  const result = await profileStore.createApiKey(apiKeyForm.value)
  if (result) {
    newApiKey.value = result.key
    apiKeyForm.value = { name: '', scopes: ['read'], expiresInDays: 90 }
  }
}

function closeCreateApiKey() {
  showCreateApiKey.value = false
  newApiKey.value = null
}

async function copyApiKey(key: string) {
  await navigator.clipboard.writeText(key)
  copiedKeyId.value = key
  setTimeout(() => { copiedKeyId.value = null }, 2000)
}

async function revokeApiKey(keyId: string) {
  await profileStore.revokeApiKey(keyId)
}

async function deleteAccount() {
  const success = await profileStore.deleteAccount()
  if (success) {
    authStore.logout()
  }
}

function formatDate(dateString: string): string {
  return new Date(dateString).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric'
  })
}

function toggleScope(scope: string) {
  const index = apiKeyForm.value.scopes.indexOf(scope)
  if (index === -1) {
    apiKeyForm.value.scopes.push(scope)
  } else {
    apiKeyForm.value.scopes.splice(index, 1)
  }
}
</script>

<template>
  <div class="min-h-screen bg-gray-50 dark:bg-gray-900 py-8">
    <div class="max-w-4xl mx-auto px-4">
      <!-- Header -->
      <div class="bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-gray-200 dark:border-gray-700 p-6 mb-6">
        <div class="flex items-start gap-6">
          <!-- Avatar -->
          <div class="relative group">
            <div
              v-if="profileStore.profile?.avatar"
              class="w-24 h-24 rounded-full bg-cover bg-center"
              :style="{ backgroundImage: `url(${profileStore.profile.avatar})` }"
            />
            <div
              v-else
              class="w-24 h-24 rounded-full bg-aegis-100 dark:bg-aegis-900 flex items-center justify-center text-2xl font-bold text-aegis-600 dark:text-aegis-400"
            >
              {{ initials }}
            </div>
            <label class="absolute inset-0 flex items-center justify-center bg-black/50 rounded-full opacity-0 group-hover:opacity-100 cursor-pointer transition-opacity">
              <CameraIcon class="h-8 w-8 text-white" />
              <input type="file" accept="image/*" class="hidden" @change="handleAvatarUpload" />
            </label>
            <button
              v-if="profileStore.profile?.avatar"
              @click="removeAvatar"
              class="absolute -bottom-1 -right-1 p-1 bg-red-500 rounded-full text-white hover:bg-red-600 transition-colors"
              title="Remove avatar"
            >
              <TrashIcon class="h-4 w-4" />
            </button>
          </div>

          <!-- Info -->
          <div class="flex-1">
            <div class="flex items-center justify-between">
              <div>
                <h1 class="text-2xl font-bold text-gray-900 dark:text-gray-100">
                  {{ profileStore.profile?.name || authStore.userName }}
                </h1>
                <p class="text-gray-500 dark:text-gray-400">{{ profileStore.profile?.email }}</p>
              </div>
              <button
                v-if="!isEditing"
                @click="startEditing"
                class="btn-secondary flex items-center gap-2"
              >
                <PencilIcon class="h-4 w-4" />
                Edit Profile
              </button>
            </div>
            <div class="flex items-center gap-4 mt-4 text-sm text-gray-600 dark:text-gray-400">
              <span class="px-2 py-1 bg-aegis-100 dark:bg-aegis-900 text-aegis-700 dark:text-aegis-300 rounded">
                {{ profileStore.profile?.role || authStore.userRole }}
              </span>
              <span v-if="profileStore.profile?.teamName">
                Team: {{ profileStore.profile.teamName }}
              </span>
              <span v-if="profileStore.profile?.createdAt">
                Joined {{ formatDate(profileStore.profile.createdAt) }}
              </span>
            </div>
          </div>
        </div>
      </div>

      <!-- Tabs -->
      <TabGroup>
        <TabList class="flex space-x-1 bg-white dark:bg-gray-800 rounded-xl p-1 shadow-sm border border-gray-200 dark:border-gray-700 mb-6">
          <Tab
            v-for="tab in tabs"
            :key="tab.name"
            v-slot="{ selected }"
            as="template"
          >
            <button
              :class="[
                'flex items-center gap-2 w-full rounded-lg py-2.5 px-4 text-sm font-medium leading-5 transition-colors',
                selected
                  ? 'bg-aegis-100 dark:bg-aegis-900 text-aegis-700 dark:text-aegis-300'
                  : 'text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-700'
              ]"
            >
              <component :is="tab.icon" class="h-5 w-5" />
              {{ tab.name }}
            </button>
          </Tab>
        </TabList>

        <TabPanels>
          <!-- Profile Tab -->
          <TabPanel>
            <div class="bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-gray-200 dark:border-gray-700 p-6">
              <div v-if="isEditing" class="space-y-4">
                <div class="grid grid-cols-2 gap-4">
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Name</label>
                    <input v-model="editForm.name" type="text" class="input w-full" />
                  </div>
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Display Name</label>
                    <input v-model="editForm.displayName" type="text" class="input w-full" />
                  </div>
                </div>
                <div>
                  <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Bio</label>
                  <textarea v-model="editForm.bio" rows="3" class="input w-full" />
                </div>
                <div class="grid grid-cols-2 gap-4">
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Department</label>
                    <input v-model="editForm.department" type="text" class="input w-full" />
                  </div>
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Location</label>
                    <input v-model="editForm.location" type="text" class="input w-full" />
                  </div>
                </div>
                <div class="flex justify-end gap-3 pt-4">
                  <button @click="cancelEditing" class="btn-secondary">Cancel</button>
                  <button @click="saveProfile" class="btn-primary" :disabled="profileStore.isLoading">
                    Save Changes
                  </button>
                </div>
              </div>
              <div v-else class="space-y-6">
                <div v-if="profileStore.profile?.bio">
                  <h3 class="text-sm font-medium text-gray-500 dark:text-gray-400 mb-1">Bio</h3>
                  <p class="text-gray-900 dark:text-gray-100">{{ profileStore.profile.bio }}</p>
                </div>
                <div class="grid grid-cols-2 gap-6">
                  <div>
                    <h3 class="text-sm font-medium text-gray-500 dark:text-gray-400 mb-1">Email</h3>
                    <p class="text-gray-900 dark:text-gray-100">{{ profileStore.profile?.email }}</p>
                  </div>
                  <div>
                    <h3 class="text-sm font-medium text-gray-500 dark:text-gray-400 mb-1">Role</h3>
                    <p class="text-gray-900 dark:text-gray-100">{{ profileStore.profile?.role }}</p>
                  </div>
                  <div v-if="profileStore.profile?.department">
                    <h3 class="text-sm font-medium text-gray-500 dark:text-gray-400 mb-1">Department</h3>
                    <p class="text-gray-900 dark:text-gray-100">{{ profileStore.profile.department }}</p>
                  </div>
                  <div v-if="profileStore.profile?.location">
                    <h3 class="text-sm font-medium text-gray-500 dark:text-gray-400 mb-1">Location</h3>
                    <p class="text-gray-900 dark:text-gray-100">{{ profileStore.profile.location }}</p>
                  </div>
                </div>
              </div>
            </div>
          </TabPanel>

          <!-- Activity Tab -->
          <TabPanel>
            <div class="bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-gray-200 dark:border-gray-700 p-6">
              <div v-if="profileStore.stats" class="space-y-6">
                <!-- Stats Grid -->
                <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
                  <div class="p-4 bg-gray-50 dark:bg-gray-700/50 rounded-lg">
                    <p class="text-2xl font-bold text-gray-900 dark:text-gray-100">{{ profileStore.stats.totalSessions }}</p>
                    <p class="text-sm text-gray-500 dark:text-gray-400">Total Sessions</p>
                  </div>
                  <div class="p-4 bg-gray-50 dark:bg-gray-700/50 rounded-lg">
                    <p class="text-2xl font-bold text-gray-900 dark:text-gray-100">{{ profileStore.stats.totalQueries }}</p>
                    <p class="text-sm text-gray-500 dark:text-gray-400">Total Queries</p>
                  </div>
                  <div class="p-4 bg-gray-50 dark:bg-gray-700/50 rounded-lg">
                    <p class="text-2xl font-bold text-gray-900 dark:text-gray-100">{{ profileStore.stats.queriesThisWeek }}</p>
                    <p class="text-sm text-gray-500 dark:text-gray-400">Queries This Week</p>
                  </div>
                  <div class="p-4 bg-gray-50 dark:bg-gray-700/50 rounded-lg">
                    <p class="text-2xl font-bold text-gray-900 dark:text-gray-100">{{ profileStore.stats.avgResponseTime }}ms</p>
                    <p class="text-sm text-gray-500 dark:text-gray-400">Avg Response Time</p>
                  </div>
                </div>

                <!-- Top Workspaces -->
                <div v-if="profileStore.stats.topWorkspaces.length > 0">
                  <h3 class="text-lg font-medium text-gray-900 dark:text-gray-100 mb-4">Top Workspaces</h3>
                  <div class="space-y-3">
                    <div
                      v-for="workspace in profileStore.stats.topWorkspaces"
                      :key="workspace.workspaceId"
                      class="flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-700/50 rounded-lg"
                    >
                      <span class="text-gray-900 dark:text-gray-100">{{ workspace.workspaceName }}</span>
                      <span class="text-sm text-gray-500 dark:text-gray-400">{{ workspace.queryCount }} queries</span>
                    </div>
                  </div>
                </div>
              </div>
              <div v-else class="text-center py-8 text-gray-500 dark:text-gray-400">
                Loading activity data...
              </div>
            </div>
          </TabPanel>

          <!-- API Keys Tab -->
          <TabPanel>
            <div class="bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-gray-200 dark:border-gray-700 p-6">
              <div class="flex items-center justify-between mb-6">
                <h3 class="text-lg font-medium text-gray-900 dark:text-gray-100">API Keys</h3>
                <button @click="showCreateApiKey = true" class="btn-primary flex items-center gap-2">
                  <PlusIcon class="h-4 w-4" />
                  Create Key
                </button>
              </div>

              <!-- New API Key Display -->
              <div v-if="newApiKey" class="mb-6 p-4 bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800 rounded-lg">
                <p class="text-sm text-green-700 dark:text-green-300 mb-2">
                  Your new API key has been created. Copy it now - you won't be able to see it again!
                </p>
                <div class="flex items-center gap-2">
                  <code class="flex-1 p-2 bg-white dark:bg-gray-800 rounded border text-sm font-mono">{{ newApiKey }}</code>
                  <button @click="copyApiKey(newApiKey)" class="btn-secondary p-2">
                    <ClipboardDocumentIcon v-if="copiedKeyId !== newApiKey" class="h-5 w-5" />
                    <CheckIcon v-else class="h-5 w-5 text-green-600" />
                  </button>
                </div>
                <button @click="closeCreateApiKey" class="mt-3 text-sm text-green-600 dark:text-green-400 hover:underline">
                  I've copied my key
                </button>
              </div>

              <!-- Create API Key Form -->
              <div v-if="showCreateApiKey && !newApiKey" class="mb-6 p-4 bg-gray-50 dark:bg-gray-700/50 rounded-lg">
                <h4 class="font-medium text-gray-900 dark:text-gray-100 mb-4">Create New API Key</h4>
                <div class="space-y-4">
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Name</label>
                    <input v-model="apiKeyForm.name" type="text" placeholder="My API Key" class="input w-full" />
                  </div>
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Scopes</label>
                    <div class="flex flex-wrap gap-2">
                      <button
                        v-for="scope in availableScopes"
                        :key="scope.value"
                        @click="toggleScope(scope.value)"
                        :class="[
                          'px-3 py-1.5 rounded-full text-sm transition-colors',
                          apiKeyForm.scopes.includes(scope.value)
                            ? 'bg-aegis-100 dark:bg-aegis-900 text-aegis-700 dark:text-aegis-300 border border-aegis-300 dark:border-aegis-700'
                            : 'bg-white dark:bg-gray-600 text-gray-700 dark:text-gray-300 border border-gray-300 dark:border-gray-500'
                        ]"
                      >
                        {{ scope.label }}
                      </button>
                    </div>
                  </div>
                  <div class="flex justify-end gap-3">
                    <button @click="showCreateApiKey = false" class="btn-secondary">Cancel</button>
                    <button @click="createApiKey" class="btn-primary" :disabled="!apiKeyForm.name || profileStore.isLoading">
                      Create Key
                    </button>
                  </div>
                </div>
              </div>

              <!-- API Keys List -->
              <div v-if="profileStore.apiKeys.length > 0" class="space-y-3">
                <div
                  v-for="key in profileStore.apiKeys"
                  :key="key.id"
                  class="flex items-center justify-between p-4 bg-gray-50 dark:bg-gray-700/50 rounded-lg"
                >
                  <div>
                    <p class="font-medium text-gray-900 dark:text-gray-100">{{ key.name }}</p>
                    <p class="text-sm text-gray-500 dark:text-gray-400">
                      {{ key.prefix }}... | Created {{ formatDate(key.createdAt) }}
                      <span v-if="key.lastUsedAt"> | Last used {{ formatDate(key.lastUsedAt) }}</span>
                    </p>
                    <div class="flex gap-1 mt-1">
                      <span
                        v-for="scope in key.scopes"
                        :key="scope"
                        class="px-1.5 py-0.5 text-xs bg-gray-200 dark:bg-gray-600 text-gray-700 dark:text-gray-300 rounded"
                      >
                        {{ scope }}
                      </span>
                    </div>
                  </div>
                  <button
                    @click="revokeApiKey(key.id)"
                    class="btn-ghost text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/20"
                  >
                    <TrashIcon class="h-5 w-5" />
                  </button>
                </div>
              </div>
              <div v-else class="text-center py-8 text-gray-500 dark:text-gray-400">
                No API keys yet. Create one to access the API programmatically.
              </div>
            </div>
          </TabPanel>

          <!-- Security Tab -->
          <TabPanel>
            <div class="space-y-6">
              <!-- Change Password -->
              <div class="bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-gray-200 dark:border-gray-700 p-6">
                <h3 class="text-lg font-medium text-gray-900 dark:text-gray-100 mb-4">Change Password</h3>
                <div v-if="showChangePassword" class="space-y-4">
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Current Password</label>
                    <input v-model="passwordForm.currentPassword" type="password" class="input w-full" />
                  </div>
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">New Password</label>
                    <input v-model="passwordForm.newPassword" type="password" class="input w-full" />
                  </div>
                  <div>
                    <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">Confirm New Password</label>
                    <input v-model="passwordForm.confirmPassword" type="password" class="input w-full" />
                  </div>
                  <div class="flex gap-3">
                    <button @click="showChangePassword = false" class="btn-secondary">Cancel</button>
                    <button @click="changePassword" class="btn-primary" :disabled="profileStore.isLoading">
                      Update Password
                    </button>
                  </div>
                </div>
                <button v-else @click="showChangePassword = true" class="btn-secondary">
                  Change Password
                </button>
              </div>

              <!-- Delete Account -->
              <div class="bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-red-200 dark:border-red-800 p-6">
                <h3 class="text-lg font-medium text-red-600 dark:text-red-400 mb-2">Danger Zone</h3>
                <p class="text-sm text-gray-600 dark:text-gray-400 mb-4">
                  Once you delete your account, there is no going back. Please be certain.
                </p>
                <button
                  v-if="!showDeleteConfirm"
                  @click="showDeleteConfirm = true"
                  class="btn-ghost text-red-600 dark:text-red-400 border border-red-300 dark:border-red-700 hover:bg-red-50 dark:hover:bg-red-900/20"
                >
                  Delete Account
                </button>
                <div v-else class="flex items-center gap-4 p-4 bg-red-50 dark:bg-red-900/20 rounded-lg">
                  <ExclamationTriangleIcon class="h-6 w-6 text-red-600 dark:text-red-400" />
                  <div class="flex-1">
                    <p class="text-sm text-red-700 dark:text-red-300">Are you absolutely sure?</p>
                  </div>
                  <button @click="showDeleteConfirm = false" class="btn-secondary text-sm">Cancel</button>
                  <button @click="deleteAccount" class="btn-primary bg-red-600 hover:bg-red-700 text-sm">
                    Yes, Delete My Account
                  </button>
                </div>
              </div>
            </div>
          </TabPanel>
        </TabPanels>
      </TabGroup>

      <!-- Error Display -->
      <div v-if="profileStore.error" class="mt-4 p-4 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
        <p class="text-red-700 dark:text-red-300">{{ profileStore.error }}</p>
      </div>
    </div>
  </div>
</template>
