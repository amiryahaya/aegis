<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/zod'
import { useWorkspaceStore } from '@/stores/workspace'
import { useAuthStore } from '@/stores/auth'
import { useBreakpoints } from '@/composables/useMediaQuery'
import { createWorkspaceSchema, type CreateWorkspaceFormData } from '@/validation/schemas'
import {
  PlusIcon,
  FolderIcon,
  DocumentTextIcon,
  MagnifyingGlassIcon,
  Cog6ToothIcon,
  TrashIcon,
  EllipsisVerticalIcon
} from '@heroicons/vue/24/outline'
import {
  Dialog,
  DialogPanel,
  DialogTitle,
  TransitionRoot,
  TransitionChild,
  Menu,
  MenuButton,
  MenuItem,
  MenuItems
} from '@headlessui/vue'
import type { Workspace } from '@/types'
import type { CreateWorkspaceRequest } from '@/types/workspace'
import { FormField } from '@/components/form'
import MobileWorkspaceCard from '@/components/mobile/MobileWorkspaceCard.vue'
import FloatingActionButton from '@/components/mobile/FloatingActionButton.vue'

const router = useRouter()
const workspaceStore = useWorkspaceStore()
const authStore = useAuthStore()
const { isMobile } = useBreakpoints()

const searchQuery = ref('')
const isCreateDialogOpen = ref(false)

// Form validation
const { defineField, handleSubmit, errors, resetForm, meta } = useForm<CreateWorkspaceFormData>({
  validationSchema: toTypedSchema(createWorkspaceSchema),
  initialValues: {
    name: '',
    description: '',
    visibility: 'private'
  }
})

const [name] = defineField('name')
const [description] = defineField('description')
const nameTouched = ref(false)
const descriptionTouched = ref(false)

const filteredWorkspaces = computed(() => {
  if (!searchQuery.value) return workspaceStore.workspaces
  const query = searchQuery.value.toLowerCase()
  return workspaceStore.workspaces.filter(ws =>
    ws.name.toLowerCase().includes(query) ||
    ws.description?.toLowerCase().includes(query)
  )
})

onMounted(async () => {
  if (authStore.user?.teamId) {
    await workspaceStore.fetchWorkspaces(authStore.user.teamId)
  }
})

// Reset form when dialog closes
watch(isCreateDialogOpen, (isOpen) => {
  if (!isOpen) {
    resetForm()
    nameTouched.value = false
    descriptionTouched.value = false
  }
})

const createWorkspace = handleSubmit(async (values) => {
  if (!authStore.user?.teamId) return

  const request: CreateWorkspaceRequest = {
    teamId: authStore.user.teamId,
    name: values.name.trim(),
    description: values.description?.trim() || undefined
  }

  const workspace = await workspaceStore.createWorkspace(request)
  if (workspace) {
    isCreateDialogOpen.value = false
    router.push(`/workspaces/${workspace.id}`)
  }
})

function openWorkspace(workspace: Workspace) {
  router.push(`/workspaces/${workspace.id}`)
}

async function deleteWorkspace(workspace: Workspace) {
  if (confirm(`Are you sure you want to delete "${workspace.name}"? This action cannot be undone.`)) {
    await workspaceStore.deleteWorkspace(workspace.id)
  }
}

function formatDate(dateString: string) {
  return new Date(dateString).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric'
  })
}
</script>

<template>
  <div class="space-y-4 md:space-y-6 p-4 md:p-6">
    <!-- Header -->
    <div class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
      <div>
        <h1 class="text-xl md:text-2xl font-bold text-gray-900 dark:text-white">Workspaces</h1>
        <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Manage your knowledge bases and data sources
        </p>
      </div>

      <!-- Desktop button -->
      <button
        class="btn-primary hidden md:inline-flex items-center gap-2"
        @click="isCreateDialogOpen = true"
      >
        <PlusIcon class="h-5 w-5" />
        New Workspace
      </button>
    </div>

    <!-- Search -->
    <div class="relative">
      <MagnifyingGlassIcon class="absolute left-3 top-1/2 h-5 w-5 -translate-y-1/2 text-gray-400" />
      <input
        v-model="searchQuery"
        type="text"
        placeholder="Search workspaces..."
        class="input w-full pl-10"
      />
    </div>

    <!-- Loading -->
    <div v-if="workspaceStore.isLoading" class="flex justify-center py-12">
      <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-aegis-600"></div>
    </div>

    <!-- Empty state -->
    <div
      v-else-if="filteredWorkspaces.length === 0"
      class="text-center py-12"
    >
      <FolderIcon class="mx-auto h-12 w-12 text-gray-400" />
      <h3 class="mt-4 text-lg font-medium text-gray-900 dark:text-white">
        {{ searchQuery ? 'No workspaces found' : 'No workspaces yet' }}
      </h3>
      <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">
        {{ searchQuery ? 'Try a different search term' : 'Create your first workspace to get started' }}
      </p>
      <button
        v-if="!searchQuery"
        class="btn-primary mt-4"
        @click="isCreateDialogOpen = true"
      >
        Create Workspace
      </button>
    </div>

    <!-- Mobile Workspace List -->
    <div v-else-if="isMobile" class="-mx-4 bg-white dark:bg-gray-800 rounded-lg overflow-hidden">
      <MobileWorkspaceCard
        v-for="workspace in filteredWorkspaces"
        :key="workspace.id"
        :workspace="workspace"
        @click="openWorkspace"
      />
    </div>

    <!-- Desktop Workspace grid -->
    <div v-else class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      <div
        v-for="workspace in filteredWorkspaces"
        :key="workspace.id"
        class="card group cursor-pointer transition-all hover:shadow-lg"
        @click="openWorkspace(workspace)"
      >
        <div class="flex items-start justify-between">
          <div class="flex items-center gap-3">
            <div class="rounded-lg bg-aegis-100 p-2 dark:bg-aegis-900/50">
              <FolderIcon class="h-6 w-6 text-aegis-600 dark:text-aegis-400" />
            </div>
            <div>
              <h3 class="font-semibold text-gray-900 dark:text-white">
                {{ workspace.name }}
              </h3>
              <p class="text-sm text-gray-500 dark:text-gray-400">
                Created {{ formatDate(workspace.createdAt) }}
              </p>
            </div>
          </div>

          <Menu as="div" class="relative" @click.stop>
            <MenuButton
              class="rounded-lg p-1 opacity-0 transition-opacity group-hover:opacity-100 hover:bg-gray-100 dark:hover:bg-gray-700"
            >
              <EllipsisVerticalIcon class="h-5 w-5 text-gray-500" />
            </MenuButton>

            <transition
              enter-active-class="transition ease-out duration-100"
              enter-from-class="transform opacity-0 scale-95"
              enter-to-class="transform opacity-100 scale-100"
              leave-active-class="transition ease-in duration-75"
              leave-from-class="transform opacity-100 scale-100"
              leave-to-class="transform opacity-0 scale-95"
            >
              <MenuItems
                class="absolute right-0 z-10 mt-2 w-48 origin-top-right rounded-lg bg-white py-1 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-800 dark:ring-gray-700"
              >
                <MenuItem v-slot="{ active }">
                  <button
                    class="flex w-full items-center gap-2 px-4 py-2 text-sm"
                    :class="active ? 'bg-gray-100 dark:bg-gray-700' : ''"
                    @click="router.push(`/workspaces/${workspace.id}/settings`)"
                  >
                    <Cog6ToothIcon class="h-4 w-4" />
                    Settings
                  </button>
                </MenuItem>
                <MenuItem v-slot="{ active }">
                  <button
                    class="flex w-full items-center gap-2 px-4 py-2 text-sm text-red-600 dark:text-red-400"
                    :class="active ? 'bg-red-50 dark:bg-red-900/30' : ''"
                    @click="deleteWorkspace(workspace)"
                  >
                    <TrashIcon class="h-4 w-4" />
                    Delete
                  </button>
                </MenuItem>
              </MenuItems>
            </transition>
          </Menu>
        </div>

        <p
          v-if="workspace.description"
          class="mt-3 text-sm text-gray-600 dark:text-gray-300 line-clamp-2"
        >
          {{ workspace.description }}
        </p>

        <div class="mt-4 flex items-center gap-4 text-sm text-gray-500 dark:text-gray-400">
          <div class="flex items-center gap-1">
            <DocumentTextIcon class="h-4 w-4" />
            {{ workspace.stats?.documentCount || 0 }} documents
          </div>
          <div class="flex items-center gap-1">
            <MagnifyingGlassIcon class="h-4 w-4" />
            {{ workspace.stats?.queryCount || 0 }} queries
          </div>
        </div>
      </div>
    </div>

    <!-- Mobile FAB -->
    <FloatingActionButton
      @click="isCreateDialogOpen = true"
    />

    <!-- Create Dialog -->
    <TransitionRoot appear :show="isCreateDialogOpen" as="template">
      <Dialog as="div" class="relative z-50" @close="isCreateDialogOpen = false">
        <TransitionChild
          as="template"
          enter="ease-out duration-300"
          enter-from="opacity-0"
          enter-to="opacity-100"
          leave="ease-in duration-200"
          leave-from="opacity-100"
          leave-to="opacity-0"
        >
          <div class="fixed inset-0 bg-black bg-opacity-25 dark:bg-opacity-50" />
        </TransitionChild>

        <div class="fixed inset-0 overflow-y-auto">
          <div class="flex min-h-full items-center justify-center p-4">
            <TransitionChild
              as="template"
              enter="ease-out duration-300"
              enter-from="opacity-0 scale-95"
              enter-to="opacity-100 scale-100"
              leave="ease-in duration-200"
              leave-from="opacity-100 scale-100"
              leave-to="opacity-0 scale-95"
            >
              <DialogPanel class="w-full max-w-md transform overflow-hidden rounded-2xl bg-white p-6 shadow-xl transition-all dark:bg-gray-800">
                <DialogTitle class="text-lg font-medium text-gray-900 dark:text-white">
                  Create New Workspace
                </DialogTitle>

                <form @submit.prevent="createWorkspace" class="mt-4 space-y-4" novalidate>
                  <FormField
                    v-model="name"
                    name="name"
                    label="Name"
                    type="text"
                    placeholder="My Workspace"
                    :error="errors.name"
                    :touched="nameTouched"
                    :required="true"
                    hint="Choose a unique name for your workspace"
                    @blur="nameTouched = true"
                  />

                  <FormField
                    v-model="description"
                    name="description"
                    label="Description"
                    type="textarea"
                    placeholder="What is this workspace for?"
                    :rows="3"
                    :error="errors.description"
                    :touched="descriptionTouched"
                    hint="Optional: Describe the purpose of this workspace"
                    @blur="descriptionTouched = true"
                  />

                  <div class="flex justify-end gap-3 pt-2">
                    <button
                      type="button"
                      class="btn-ghost"
                      @click="isCreateDialogOpen = false"
                    >
                      Cancel
                    </button>
                    <button
                      type="submit"
                      class="btn-primary"
                      :disabled="!meta.valid"
                    >
                      Create
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
