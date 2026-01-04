<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useWorkspaceStore } from '@/stores/workspace'
import { useToast } from '@/composables/useToast'
import type { ShareableLink, WorkspaceRole, CreateShareableLinkRequest } from '@/types/workspace'
import {
  LinkIcon,
  PlusIcon,
  TrashIcon,
  ClipboardDocumentIcon,
  ClockIcon,
  UsersIcon,
  LockClosedIcon,
  CheckIcon,
  ChevronUpDownIcon,
  EyeIcon,
  ChatBubbleLeftIcon,
  PencilSquareIcon
} from '@heroicons/vue/24/outline'
import {
  Dialog,
  DialogPanel,
  DialogTitle,
  TransitionRoot,
  TransitionChild,
  Listbox,
  ListboxButton,
  ListboxOptions,
  ListboxOption,
  Switch
} from '@headlessui/vue'

const props = defineProps<{
  workspaceId: string
}>()

const workspaceStore = useWorkspaceStore()
const toast = useToast()

const loading = ref(false)
const links = ref<ShareableLink[]>([])
const isCreateDialogOpen = ref(false)
const creating = ref(false)
const copiedLinkId = ref<string | null>(null)

// Create link form
const newLinkRole = ref<WorkspaceRole>('Viewer')
const newLinkExpiration = ref<string>('7d')
const newLinkMaxUses = ref<number | null>(null)
const newLinkPassword = ref('')
const usePassword = ref(false)

const roles: { value: WorkspaceRole; label: string; icon: typeof EyeIcon }[] = [
  { value: 'Viewer', label: 'Viewer', icon: EyeIcon },
  { value: 'Commenter', label: 'Commenter', icon: ChatBubbleLeftIcon },
  { value: 'Editor', label: 'Editor', icon: PencilSquareIcon }
]

const expirationOptions = [
  { value: '1h', label: '1 hour' },
  { value: '24h', label: '24 hours' },
  { value: '7d', label: '7 days' },
  { value: '30d', label: '30 days' },
  { value: 'never', label: 'Never expires' }
]

const maxUsesOptions = [
  { value: null, label: 'Unlimited' },
  { value: 1, label: '1 use' },
  { value: 5, label: '5 uses' },
  { value: 10, label: '10 uses' },
  { value: 25, label: '25 uses' },
  { value: 100, label: '100 uses' }
]

onMounted(async () => {
  await fetchLinks()
})

async function fetchLinks() {
  loading.value = true
  try {
    links.value = await workspaceStore.fetchShareableLinks(props.workspaceId)
  } catch (error) {
    toast.error('Error', 'Failed to load shareable links')
  } finally {
    loading.value = false
  }
}

function getExpirationDate(expiration: string): string | undefined {
  if (expiration === 'never') return undefined

  const now = new Date()
  const amount = parseInt(expiration)
  const unit = expiration.replace(/\d/g, '')

  switch (unit) {
    case 'h':
      now.setHours(now.getHours() + amount)
      break
    case 'd':
      now.setDate(now.getDate() + amount)
      break
  }

  return now.toISOString()
}

async function createLink() {
  creating.value = true
  try {
    const request: CreateShareableLinkRequest = {
      role: newLinkRole.value,
      expiresAt: getExpirationDate(newLinkExpiration.value),
      maxUses: newLinkMaxUses.value || undefined,
      password: usePassword.value && newLinkPassword.value ? newLinkPassword.value : undefined
    }

    const link = await workspaceStore.createShareableLink(props.workspaceId, request)
    if (link) {
      links.value.push(link)
      toast.success('Link created', 'Shareable link has been created')

      // Auto-copy to clipboard
      await copyLink(link)
    }

    isCreateDialogOpen.value = false
    resetForm()
  } catch (error) {
    toast.error('Error', 'Failed to create shareable link')
  } finally {
    creating.value = false
  }
}

function resetForm() {
  newLinkRole.value = 'Viewer'
  newLinkExpiration.value = '7d'
  newLinkMaxUses.value = null
  newLinkPassword.value = ''
  usePassword.value = false
}

async function copyLink(link: ShareableLink) {
  const url = `${window.location.origin}/invite/${link.token}`
  try {
    await navigator.clipboard.writeText(url)
    copiedLinkId.value = link.id
    toast.success('Copied!', 'Link copied to clipboard')
    setTimeout(() => {
      copiedLinkId.value = null
    }, 2000)
  } catch (error) {
    toast.error('Error', 'Failed to copy link')
  }
}

async function revokeLink(linkId: string) {
  if (!confirm('Are you sure you want to revoke this link? Anyone with this link will no longer be able to access the workspace.')) return

  try {
    await workspaceStore.revokeShareableLink(props.workspaceId, linkId)
    links.value = links.value.filter(l => l.id !== linkId)
    toast.success('Link revoked', 'The shareable link has been revoked')
  } catch (error) {
    toast.error('Error', 'Failed to revoke link')
  }
}

function formatDate(dateStr: string) {
  return new Date(dateStr).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

function isExpired(link: ShareableLink): boolean {
  if (!link.expiresAt) return false
  return new Date(link.expiresAt) < new Date()
}

function isMaxedOut(link: ShareableLink): boolean {
  if (!link.maxUses) return false
  return link.useCount >= link.maxUses
}

const activeLinks = computed(() => links.value.filter(l => l.isActive && !isExpired(l) && !isMaxedOut(l)))
const inactiveLinks = computed(() => links.value.filter(l => !l.isActive || isExpired(l) || isMaxedOut(l)))

function getRoleIcon(role: WorkspaceRole) {
  return roles.find(r => r.value === role)?.icon || EyeIcon
}
</script>

<template>
  <div class="bg-white rounded-lg border border-gray-200 p-6 dark:bg-gray-800 dark:border-gray-700">
    <div class="flex items-center justify-between mb-4">
      <div>
        <h2 class="text-lg font-semibold text-gray-900 dark:text-white">
          Shareable Links
        </h2>
        <p class="text-sm text-gray-500 dark:text-gray-400">
          Create invite links to share this workspace
        </p>
      </div>
      <button
        class="btn-primary inline-flex items-center gap-2"
        @click="isCreateDialogOpen = true"
      >
        <PlusIcon class="h-4 w-4" />
        Create Link
      </button>
    </div>

    <!-- Links List -->
    <div v-if="loading" class="py-8 text-center">
      <div class="animate-spin rounded-full h-6 w-6 border-b-2 border-aegis-600 mx-auto"></div>
    </div>

    <div v-else-if="links.length === 0" class="text-center py-8">
      <LinkIcon class="mx-auto h-12 w-12 text-gray-400" />
      <p class="mt-2 text-gray-500 dark:text-gray-400">No shareable links yet</p>
      <button
        class="btn-primary mt-4"
        @click="isCreateDialogOpen = true"
      >
        Create First Link
      </button>
    </div>

    <div v-else class="space-y-4">
      <!-- Active Links -->
      <div v-if="activeLinks.length > 0">
        <h3 class="text-sm font-medium text-gray-500 dark:text-gray-400 mb-2">Active Links</h3>
        <div class="space-y-2">
          <div
            v-for="link in activeLinks"
            :key="link.id"
            class="flex items-center justify-between p-3 rounded-lg bg-gray-50 dark:bg-gray-700/50"
          >
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 rounded-full bg-green-100 dark:bg-green-900/50 flex items-center justify-center">
                <LinkIcon class="h-5 w-5 text-green-600 dark:text-green-400" />
              </div>
              <div>
                <div class="flex items-center gap-2">
                  <span class="font-medium text-gray-900 dark:text-white flex items-center gap-1">
                    <component :is="getRoleIcon(link.role)" class="h-4 w-4" />
                    {{ link.role }}
                  </span>
                  <span v-if="link.maxUses" class="text-xs text-gray-500 dark:text-gray-400 flex items-center gap-1">
                    <UsersIcon class="h-3 w-3" />
                    {{ link.useCount }}/{{ link.maxUses }}
                  </span>
                </div>
                <div class="flex items-center gap-2 text-xs text-gray-500 dark:text-gray-400">
                  <span v-if="link.expiresAt" class="flex items-center gap-1">
                    <ClockIcon class="h-3 w-3" />
                    Expires {{ formatDate(link.expiresAt) }}
                  </span>
                  <span v-else class="flex items-center gap-1">
                    <ClockIcon class="h-3 w-3" />
                    Never expires
                  </span>
                </div>
              </div>
            </div>

            <div class="flex items-center gap-2">
              <button
                class="btn-ghost p-2"
                @click="copyLink(link)"
              >
                <ClipboardDocumentIcon v-if="copiedLinkId !== link.id" class="h-4 w-4" />
                <CheckIcon v-else class="h-4 w-4 text-green-600" />
              </button>
              <button
                class="btn-ghost p-2 text-red-600 hover:bg-red-50 dark:hover:bg-red-900/20"
                @click="revokeLink(link.id)"
              >
                <TrashIcon class="h-4 w-4" />
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Inactive Links -->
      <div v-if="inactiveLinks.length > 0">
        <h3 class="text-sm font-medium text-gray-500 dark:text-gray-400 mb-2">Expired/Revoked Links</h3>
        <div class="space-y-2 opacity-60">
          <div
            v-for="link in inactiveLinks"
            :key="link.id"
            class="flex items-center justify-between p-3 rounded-lg bg-gray-50 dark:bg-gray-700/50"
          >
            <div class="flex items-center gap-3">
              <div class="w-10 h-10 rounded-full bg-gray-200 dark:bg-gray-600 flex items-center justify-center">
                <LinkIcon class="h-5 w-5 text-gray-400" />
              </div>
              <div>
                <div class="flex items-center gap-2">
                  <span class="font-medium text-gray-500 dark:text-gray-400">{{ link.role }}</span>
                  <span class="text-xs px-1.5 py-0.5 rounded bg-gray-200 text-gray-600 dark:bg-gray-600 dark:text-gray-300">
                    {{ !link.isActive ? 'Revoked' : isExpired(link) ? 'Expired' : 'Max uses reached' }}
                  </span>
                </div>
                <p class="text-xs text-gray-400">
                  Created {{ formatDate(link.createdAt) }}
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Create Link Dialog -->
    <TransitionRoot appear :show="isCreateDialogOpen" as="template">
      <Dialog as="div" class="relative z-50" @close="isCreateDialogOpen = false">
        <TransitionChild
          enter="ease-out duration-300"
          enter-from="opacity-0"
          enter-to="opacity-100"
          leave="ease-in duration-200"
          leave-from="opacity-100"
          leave-to="opacity-0"
        >
          <div class="fixed inset-0 bg-black/30 backdrop-blur-sm" />
        </TransitionChild>

        <div class="fixed inset-0 overflow-y-auto">
          <div class="flex min-h-full items-center justify-center p-4">
            <TransitionChild
              enter="ease-out duration-300"
              enter-from="opacity-0 scale-95"
              enter-to="opacity-100 scale-100"
              leave="ease-in duration-200"
              leave-from="opacity-100 scale-100"
              leave-to="opacity-0 scale-95"
            >
              <DialogPanel class="w-full max-w-md transform overflow-hidden rounded-2xl bg-white p-6 shadow-xl transition-all dark:bg-gray-800">
                <DialogTitle class="text-lg font-semibold text-gray-900 dark:text-white">
                  Create Shareable Link
                </DialogTitle>

                <div class="mt-4 space-y-4">
                  <!-- Role -->
                  <div>
                    <label class="label">Access Level</label>
                    <Listbox v-model="newLinkRole">
                      <div class="relative mt-1">
                        <ListboxButton class="input w-full text-left flex items-center justify-between">
                          <div class="flex items-center gap-2">
                            <component :is="getRoleIcon(newLinkRole)" class="h-4 w-4 text-gray-400" />
                            <span>{{ roles.find(r => r.value === newLinkRole)?.label }}</span>
                          </div>
                          <ChevronUpDownIcon class="h-4 w-4 text-gray-400" />
                        </ListboxButton>
                        <ListboxOptions class="absolute z-10 mt-1 w-full bg-white rounded-md shadow-lg border border-gray-200 dark:bg-gray-800 dark:border-gray-700">
                          <ListboxOption
                            v-for="role in roles"
                            :key="role.value"
                            :value="role.value"
                            v-slot="{ selected, active }"
                          >
                            <div
                              class="cursor-pointer select-none px-4 py-3 flex items-center justify-between"
                              :class="active ? 'bg-gray-100 dark:bg-gray-700' : ''"
                            >
                              <div class="flex items-center gap-2">
                                <component :is="role.icon" class="h-4 w-4 text-gray-400" />
                                <span class="font-medium text-gray-900 dark:text-white">{{ role.label }}</span>
                              </div>
                              <CheckIcon v-if="selected" class="h-4 w-4 text-aegis-600" />
                            </div>
                          </ListboxOption>
                        </ListboxOptions>
                      </div>
                    </Listbox>
                  </div>

                  <!-- Expiration -->
                  <div>
                    <label class="label">Link Expiration</label>
                    <select v-model="newLinkExpiration" class="input w-full">
                      <option v-for="opt in expirationOptions" :key="opt.value" :value="opt.value">
                        {{ opt.label }}
                      </option>
                    </select>
                  </div>

                  <!-- Max Uses -->
                  <div>
                    <label class="label">Maximum Uses</label>
                    <select v-model="newLinkMaxUses" class="input w-full">
                      <option v-for="opt in maxUsesOptions" :key="String(opt.value)" :value="opt.value">
                        {{ opt.label }}
                      </option>
                    </select>
                  </div>

                  <!-- Password Protection -->
                  <div class="space-y-3">
                    <div class="flex items-center justify-between">
                      <div class="flex items-center gap-2">
                        <LockClosedIcon class="h-4 w-4 text-gray-400" />
                        <span class="text-sm font-medium text-gray-900 dark:text-white">Password Protection</span>
                      </div>
                      <Switch
                        v-model="usePassword"
                        :class="usePassword ? 'bg-aegis-600' : 'bg-gray-200 dark:bg-gray-700'"
                        class="relative inline-flex h-6 w-11 items-center rounded-full transition-colors"
                      >
                        <span
                          :class="usePassword ? 'translate-x-6' : 'translate-x-1'"
                          class="inline-block h-4 w-4 transform rounded-full bg-white transition-transform"
                        />
                      </Switch>
                    </div>
                    <input
                      v-if="usePassword"
                      v-model="newLinkPassword"
                      type="password"
                      class="input w-full"
                      placeholder="Enter password"
                    />
                  </div>
                </div>

                <div class="mt-6 flex justify-end gap-3">
                  <button
                    class="btn-ghost"
                    @click="isCreateDialogOpen = false"
                  >
                    Cancel
                  </button>
                  <button
                    class="btn-primary"
                    :disabled="creating"
                    @click="createLink"
                  >
                    <span v-if="creating">Creating...</span>
                    <span v-else>Create Link</span>
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
