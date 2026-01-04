<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useWorkspaceStore } from '@/stores/workspace'
import { useToast } from '@/composables/useToast'
import type { WorkspaceShare, WorkspaceRole } from '@/types/workspace'
import {
  UserPlusIcon,
  TrashIcon,
  UserIcon,
  ShieldCheckIcon,
  PencilSquareIcon,
  ChatBubbleLeftIcon,
  EyeIcon,
  ChevronUpDownIcon,
  CheckIcon
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
  Menu,
  MenuButton,
  MenuItems,
  MenuItem
} from '@headlessui/vue'

const props = defineProps<{
  workspaceId: string
}>()

const workspaceStore = useWorkspaceStore()
const toast = useToast()

const loading = ref(false)
const shares = ref<WorkspaceShare[]>([])
const isAddDialogOpen = ref(false)
const addingUser = ref(false)

// Add user form
const newUserEmail = ref('')
const newUserRole = ref<WorkspaceRole>('Viewer')

const roles: { value: WorkspaceRole; label: string; description: string; icon: typeof EyeIcon }[] = [
  { value: 'Viewer', label: 'Viewer', description: 'Can view documents and query the workspace', icon: EyeIcon },
  { value: 'Commenter', label: 'Commenter', description: 'Can view and add comments', icon: ChatBubbleLeftIcon },
  { value: 'Editor', label: 'Editor', description: 'Can edit documents and data sources', icon: PencilSquareIcon },
  { value: 'Admin', label: 'Admin', description: 'Can manage members and settings', icon: ShieldCheckIcon }
]

const currentUserRole = computed(() => {
  // TODO: Get from auth store
  return 'Owner' as WorkspaceRole
})

const canManageMembers = computed(() => {
  return ['Owner', 'Admin'].includes(currentUserRole.value)
})

onMounted(async () => {
  await fetchShares()
})

async function fetchShares() {
  loading.value = true
  try {
    shares.value = await workspaceStore.fetchShares(props.workspaceId)
  } catch (error) {
    toast.error('Error', 'Failed to load workspace members')
  } finally {
    loading.value = false
  }
}

async function addUser() {
  if (!newUserEmail.value.trim()) return

  addingUser.value = true
  try {
    // TODO: Call API to share workspace with user
    // await workspaceStore.shareWorkspace(props.workspaceId, newUserEmail.value, newUserRole.value)

    // Mock for now - add to local state
    const mockShare: WorkspaceShare = {
      id: `share-${Date.now()}`,
      workspaceId: props.workspaceId,
      userId: `user-${Date.now()}`,
      role: newUserRole.value,
      createdAt: new Date().toISOString(),
      createdBy: 'current-user'
    }
    shares.value.push(mockShare)

    toast.success('User added', `${newUserEmail.value} has been added as ${newUserRole.value}`)
    isAddDialogOpen.value = false
    newUserEmail.value = ''
    newUserRole.value = 'Viewer'
  } catch (error) {
    toast.error('Error', 'Failed to add user')
  } finally {
    addingUser.value = false
  }
}

async function updateRole(shareId: string, newRole: WorkspaceRole) {
  try {
    // TODO: Call API to update share role
    const index = shares.value.findIndex(s => s.id === shareId)
    if (index !== -1) {
      shares.value[index] = { ...shares.value[index], role: newRole }
    }
    toast.success('Role updated', 'Member role has been updated')
  } catch (error) {
    toast.error('Error', 'Failed to update role')
  }
}

async function removeUser(shareId: string) {
  if (!confirm('Are you sure you want to remove this member?')) return

  try {
    // TODO: Call API to remove share
    shares.value = shares.value.filter(s => s.id !== shareId)
    toast.success('Member removed', 'User has been removed from the workspace')
  } catch (error) {
    toast.error('Error', 'Failed to remove member')
  }
}

function getRoleIcon(role: WorkspaceRole) {
  return roles.find(r => r.value === role)?.icon || UserIcon
}

function formatDate(dateStr: string) {
  return new Date(dateStr).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric'
  })
}
</script>

<template>
  <div class="bg-white rounded-lg border border-gray-200 p-6 dark:bg-gray-800 dark:border-gray-700">
    <div class="flex items-center justify-between mb-4">
      <h2 class="text-lg font-semibold text-gray-900 dark:text-white">
        Workspace Members
      </h2>
      <button
        v-if="canManageMembers"
        class="btn-primary inline-flex items-center gap-2"
        @click="isAddDialogOpen = true"
      >
        <UserPlusIcon class="h-4 w-4" />
        Add Member
      </button>
    </div>

    <!-- Members List -->
    <div v-if="loading" class="py-8 text-center">
      <div class="animate-spin rounded-full h-6 w-6 border-b-2 border-aegis-600 mx-auto"></div>
    </div>

    <div v-else-if="shares.length === 0" class="text-center py-8">
      <UserIcon class="mx-auto h-12 w-12 text-gray-400" />
      <p class="mt-2 text-gray-500 dark:text-gray-400">No members yet</p>
      <button
        v-if="canManageMembers"
        class="btn-primary mt-4"
        @click="isAddDialogOpen = true"
      >
        Add First Member
      </button>
    </div>

    <div v-else class="space-y-2">
      <div
        v-for="share in shares"
        :key="share.id"
        class="flex items-center justify-between p-3 rounded-lg hover:bg-gray-50 dark:hover:bg-gray-700/50"
      >
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-full bg-aegis-100 dark:bg-aegis-900/50 flex items-center justify-center">
            <UserIcon class="h-5 w-5 text-aegis-600 dark:text-aegis-400" />
          </div>
          <div>
            <p class="font-medium text-gray-900 dark:text-white">
              User {{ share.userId.slice(-4) }}
            </p>
            <p class="text-sm text-gray-500 dark:text-gray-400">
              Added {{ formatDate(share.createdAt) }}
            </p>
          </div>
        </div>

        <div class="flex items-center gap-2">
          <!-- Role Selector -->
          <Menu as="div" class="relative">
            <MenuButton
              class="inline-flex items-center gap-1 px-3 py-1.5 rounded-lg text-sm font-medium bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-300 dark:hover:bg-gray-600"
              :disabled="!canManageMembers"
            >
              <component :is="getRoleIcon(share.role)" class="h-4 w-4" />
              {{ share.role }}
              <ChevronUpDownIcon v-if="canManageMembers" class="h-4 w-4" />
            </MenuButton>
            <transition
              enter-active-class="transition ease-out duration-100"
              enter-from-class="transform opacity-0 scale-95"
              enter-to-class="transform opacity-100 scale-100"
              leave-active-class="transition ease-in duration-75"
              leave-from-class="transform opacity-100 scale-100"
              leave-to-class="transform opacity-0 scale-95"
            >
              <MenuItems class="absolute right-0 z-10 mt-2 w-56 origin-top-right rounded-lg bg-white shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-800 dark:ring-gray-700">
                <div class="py-1">
                  <MenuItem
                    v-for="role in roles"
                    :key="role.value"
                    v-slot="{ active }"
                    @click="updateRole(share.id, role.value)"
                  >
                    <button
                      class="w-full flex items-start gap-3 px-4 py-2 text-left"
                      :class="active ? 'bg-gray-100 dark:bg-gray-700' : ''"
                    >
                      <component :is="role.icon" class="h-5 w-5 mt-0.5 text-gray-400" />
                      <div>
                        <div class="flex items-center gap-2">
                          <span class="font-medium text-gray-900 dark:text-white">{{ role.label }}</span>
                          <CheckIcon v-if="share.role === role.value" class="h-4 w-4 text-aegis-600" />
                        </div>
                        <p class="text-xs text-gray-500 dark:text-gray-400">{{ role.description }}</p>
                      </div>
                    </button>
                  </MenuItem>
                </div>
              </MenuItems>
            </transition>
          </Menu>

          <!-- Remove Button -->
          <button
            v-if="canManageMembers"
            class="p-2 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-lg dark:hover:bg-red-900/20"
            @click="removeUser(share.id)"
          >
            <TrashIcon class="h-4 w-4" />
          </button>
        </div>
      </div>
    </div>

    <!-- Add User Dialog -->
    <TransitionRoot appear :show="isAddDialogOpen" as="template">
      <Dialog as="div" class="relative z-50" @close="isAddDialogOpen = false">
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
                  Add Member
                </DialogTitle>

                <div class="mt-4 space-y-4">
                  <div>
                    <label class="label">Email Address</label>
                    <input
                      v-model="newUserEmail"
                      type="email"
                      class="input w-full"
                      placeholder="user@example.com"
                    />
                  </div>

                  <div>
                    <label class="label">Role</label>
                    <Listbox v-model="newUserRole">
                      <div class="relative mt-1">
                        <ListboxButton class="input w-full text-left flex items-center justify-between">
                          <div class="flex items-center gap-2">
                            <component :is="getRoleIcon(newUserRole)" class="h-4 w-4 text-gray-400" />
                            <span>{{ roles.find(r => r.value === newUserRole)?.label }}</span>
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
                              class="cursor-pointer select-none px-4 py-3"
                              :class="active ? 'bg-gray-100 dark:bg-gray-700' : ''"
                            >
                              <div class="flex items-center justify-between">
                                <div class="flex items-center gap-2">
                                  <component :is="role.icon" class="h-4 w-4 text-gray-400" />
                                  <span class="font-medium text-gray-900 dark:text-white">{{ role.label }}</span>
                                </div>
                                <CheckIcon v-if="selected" class="h-4 w-4 text-aegis-600" />
                              </div>
                              <p class="text-sm text-gray-500 dark:text-gray-400 ml-6">{{ role.description }}</p>
                            </div>
                          </ListboxOption>
                        </ListboxOptions>
                      </div>
                    </Listbox>
                  </div>
                </div>

                <div class="mt-6 flex justify-end gap-3">
                  <button
                    class="btn-ghost"
                    @click="isAddDialogOpen = false"
                  >
                    Cancel
                  </button>
                  <button
                    class="btn-primary"
                    :disabled="!newUserEmail.trim() || addingUser"
                    @click="addUser"
                  >
                    <span v-if="addingUser">Adding...</span>
                    <span v-else>Add Member</span>
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
