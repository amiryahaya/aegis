<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { TabGroup, TabList, Tab, TabPanels, TabPanel, Listbox, ListboxButton, ListboxOptions, ListboxOption } from '@headlessui/vue'
import {
  ClockIcon,
  FunnelIcon,
  CheckCircleIcon,
  ChevronDownIcon,
  EyeIcon,
  UserGroupIcon,
  DocumentTextIcon,
  ChatBubbleLeftIcon,
  MagnifyingGlassIcon,
  ShareIcon,
  BellIcon,
  ArrowPathIcon,
  UserCircleIcon,
  FolderIcon
} from '@heroicons/vue/24/outline'
import { useActivityStore } from '@/stores/activity'
import { useWorkspaceStore } from '@/stores/workspace'
import type { FeedActivityType, ActivityVerb, Activity } from '@/types/activity'
import { getActivityColor } from '@/types/activity'

const activityStore = useActivityStore()
const workspaceStore = useWorkspaceStore()

const selectedTab = ref(0)
const selectedWorkspace = ref<string | null>(null)
const selectedType = ref<FeedActivityType | null>(null)

const activityTypes: { value: FeedActivityType | null; label: string }[] = [
  { value: null, label: 'All Types' },
  { value: 'Resource', label: 'Resources' },
  { value: 'Collaboration', label: 'Collaboration' },
  { value: 'Comment', label: 'Comments' },
  { value: 'Query', label: 'Queries' },
  { value: 'User', label: 'User Actions' },
  { value: 'System', label: 'System' }
]

const tabs = [
  { name: 'All Activity', icon: ClockIcon },
  { name: 'My Activity', icon: UserCircleIcon },
  { name: 'Following', icon: BellIcon }
]

const workspaces = computed(() => [
  { id: null, name: 'All Workspaces' },
  ...workspaceStore.workspaces.map(w => ({ id: w.id, name: w.name }))
])

onMounted(async () => {
  await Promise.all([
    activityStore.fetchPersonalizedFeed(),
    activityStore.fetchUnseenCount(),
    workspaceStore.fetchWorkspaces()
  ])
})

async function loadActivities() {
  activityStore.setFilter({
    type: selectedType.value || undefined
  })

  if (selectedTab.value === 0) {
    // All activity
    if (selectedWorkspace.value) {
      await activityStore.fetchActivities(selectedWorkspace.value)
    } else {
      await activityStore.fetchPersonalizedFeed()
    }
  } else if (selectedTab.value === 1) {
    // My activity
    await activityStore.fetchPersonalizedFeed({ includeOwnActivities: true })
  } else {
    // Following
    await activityStore.fetchPersonalizedFeed({ onlySubscribed: true })
  }
}

async function handleTabChange(index: number) {
  selectedTab.value = index
  await loadActivities()
}

async function handleWorkspaceChange() {
  await loadActivities()
}

async function handleTypeChange() {
  await loadActivities()
}

async function loadMore() {
  await activityStore.loadMore()
}

async function markAllAsSeen() {
  await activityStore.markAsSeen()
}

function getActivityIcon(activity: Activity) {
  const verb = activity.verb
  const iconMap: Record<ActivityVerb, typeof DocumentTextIcon> = {
    Created: DocumentTextIcon,
    Updated: DocumentTextIcon,
    Deleted: DocumentTextIcon,
    Shared: ShareIcon,
    Unshared: ShareIcon,
    Commented: ChatBubbleLeftIcon,
    Replied: ChatBubbleLeftIcon,
    Resolved: CheckCircleIcon,
    Reopened: ArrowPathIcon,
    Mentioned: ChatBubbleLeftIcon,
    Reacted: ChatBubbleLeftIcon,
    Viewed: EyeIcon,
    Downloaded: DocumentTextIcon,
    Exported: DocumentTextIcon,
    Uploaded: DocumentTextIcon,
    Joined: UserGroupIcon,
    Left: UserGroupIcon,
    Invited: UserGroupIcon,
    Removed: UserGroupIcon,
    Queried: MagnifyingGlassIcon,
    Answered: ChatBubbleLeftIcon,
    Pinned: DocumentTextIcon,
    Unpinned: DocumentTextIcon,
    Archived: FolderIcon,
    Restored: FolderIcon,
    Transferred: ShareIcon
  }
  return iconMap[verb] || DocumentTextIcon
}

function formatTime(dateString: string): string {
  const date = new Date(dateString)
  const now = new Date()
  const diffInSeconds = Math.floor((now.getTime() - date.getTime()) / 1000)

  if (diffInSeconds < 60) return 'just now'
  if (diffInSeconds < 3600) return `${Math.floor(diffInSeconds / 60)}m ago`
  if (diffInSeconds < 86400) return `${Math.floor(diffInSeconds / 3600)}h ago`
  if (diffInSeconds < 604800) return `${Math.floor(diffInSeconds / 86400)}d ago`

  return date.toLocaleDateString()
}

function getColorClasses(activity: Activity): string {
  const color = getActivityColor(activity.type)
  const colorClasses: Record<string, string> = {
    blue: 'bg-blue-100 text-blue-600 dark:bg-blue-900/50 dark:text-blue-400',
    purple: 'bg-purple-100 text-purple-600 dark:bg-purple-900/50 dark:text-purple-400',
    green: 'bg-green-100 text-green-600 dark:bg-green-900/50 dark:text-green-400',
    yellow: 'bg-yellow-100 text-yellow-600 dark:bg-yellow-900/50 dark:text-yellow-400',
    gray: 'bg-gray-100 text-gray-600 dark:bg-gray-900/50 dark:text-gray-400',
    indigo: 'bg-indigo-100 text-indigo-600 dark:bg-indigo-900/50 dark:text-indigo-400',
    red: 'bg-red-100 text-red-600 dark:bg-red-900/50 dark:text-red-400'
  }
  return colorClasses[color] || colorClasses.gray
}
</script>

<template>
  <div class="p-6 max-w-5xl mx-auto">
    <!-- Header -->
    <div class="flex items-center justify-between mb-6">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Activity Feed</h1>
        <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Stay updated on what's happening across your workspaces
        </p>
      </div>

      <div class="flex items-center gap-3">
        <span v-if="activityStore.unseenCount > 0" class="text-sm text-gray-500 dark:text-gray-400">
          {{ activityStore.unseenCount }} unseen
        </span>
        <button
          v-if="activityStore.unseenCount > 0"
          class="btn-secondary text-sm"
          @click="markAllAsSeen"
        >
          <CheckCircleIcon class="h-4 w-4 mr-1" />
          Mark all as seen
        </button>
      </div>
    </div>

    <!-- Tabs -->
    <TabGroup :selected-index="selectedTab" @change="handleTabChange">
      <div class="flex items-center justify-between mb-4">
        <TabList class="flex space-x-1 rounded-lg bg-gray-100 dark:bg-gray-800 p-1">
          <Tab
            v-for="tab in tabs"
            :key="tab.name"
            v-slot="{ selected }"
            as="template"
          >
            <button
              class="flex items-center gap-2 rounded-md px-3 py-2 text-sm font-medium transition-colors"
              :class="[
                selected
                  ? 'bg-white dark:bg-gray-700 text-gray-900 dark:text-white shadow'
                  : 'text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white'
              ]"
            >
              <component :is="tab.icon" class="h-4 w-4" />
              {{ tab.name }}
            </button>
          </Tab>
        </TabList>

        <!-- Filters -->
        <div class="flex items-center gap-3">
          <!-- Workspace Filter -->
          <Listbox v-model="selectedWorkspace" @update:model-value="handleWorkspaceChange">
            <div class="relative">
              <ListboxButton class="relative w-48 cursor-pointer rounded-lg bg-white dark:bg-gray-800 py-2 pl-3 pr-10 text-left border border-gray-300 dark:border-gray-600 focus:outline-none focus:ring-2 focus:ring-aegis-500 text-sm">
                <span class="block truncate">
                  {{ workspaces.find(w => w.id === selectedWorkspace)?.name || 'All Workspaces' }}
                </span>
                <span class="absolute inset-y-0 right-0 flex items-center pr-2">
                  <ChevronDownIcon class="h-4 w-4 text-gray-400" />
                </span>
              </ListboxButton>

              <transition
                leave-active-class="transition duration-100 ease-in"
                leave-from-class="opacity-100"
                leave-to-class="opacity-0"
              >
                <ListboxOptions class="absolute z-10 mt-1 max-h-60 w-full overflow-auto rounded-md bg-white dark:bg-gray-800 py-1 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none text-sm">
                  <ListboxOption
                    v-for="workspace in workspaces"
                    :key="workspace.id ?? 'all'"
                    v-slot="{ active, selected }"
                    :value="workspace.id"
                  >
                    <li
                      class="relative cursor-pointer select-none py-2 pl-10 pr-4"
                      :class="[
                        active ? 'bg-aegis-100 dark:bg-aegis-900/50 text-aegis-900 dark:text-aegis-100' : 'text-gray-900 dark:text-gray-100'
                      ]"
                    >
                      <span :class="['block truncate', selected ? 'font-medium' : 'font-normal']">
                        {{ workspace.name }}
                      </span>
                      <span v-if="selected" class="absolute inset-y-0 left-0 flex items-center pl-3 text-aegis-600">
                        <CheckCircleIcon class="h-4 w-4" />
                      </span>
                    </li>
                  </ListboxOption>
                </ListboxOptions>
              </transition>
            </div>
          </Listbox>

          <!-- Type Filter -->
          <Listbox v-model="selectedType" @update:model-value="handleTypeChange">
            <div class="relative">
              <ListboxButton class="relative w-40 cursor-pointer rounded-lg bg-white dark:bg-gray-800 py-2 pl-3 pr-10 text-left border border-gray-300 dark:border-gray-600 focus:outline-none focus:ring-2 focus:ring-aegis-500 text-sm">
                <span class="flex items-center gap-2">
                  <FunnelIcon class="h-4 w-4 text-gray-400" />
                  <span class="truncate">
                    {{ activityTypes.find(t => t.value === selectedType)?.label || 'All Types' }}
                  </span>
                </span>
                <span class="absolute inset-y-0 right-0 flex items-center pr-2">
                  <ChevronDownIcon class="h-4 w-4 text-gray-400" />
                </span>
              </ListboxButton>

              <transition
                leave-active-class="transition duration-100 ease-in"
                leave-from-class="opacity-100"
                leave-to-class="opacity-0"
              >
                <ListboxOptions class="absolute z-10 mt-1 max-h-60 w-full overflow-auto rounded-md bg-white dark:bg-gray-800 py-1 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none text-sm">
                  <ListboxOption
                    v-for="type in activityTypes"
                    :key="type.value ?? 'all'"
                    v-slot="{ active, selected }"
                    :value="type.value"
                  >
                    <li
                      class="relative cursor-pointer select-none py-2 pl-10 pr-4"
                      :class="[
                        active ? 'bg-aegis-100 dark:bg-aegis-900/50 text-aegis-900 dark:text-aegis-100' : 'text-gray-900 dark:text-gray-100'
                      ]"
                    >
                      <span :class="['block truncate', selected ? 'font-medium' : 'font-normal']">
                        {{ type.label }}
                      </span>
                      <span v-if="selected" class="absolute inset-y-0 left-0 flex items-center pl-3 text-aegis-600">
                        <CheckCircleIcon class="h-4 w-4" />
                      </span>
                    </li>
                  </ListboxOption>
                </ListboxOptions>
              </transition>
            </div>
          </Listbox>
        </div>
      </div>

      <TabPanels>
        <TabPanel v-for="tab in tabs" :key="tab.name">
          <!-- Activity List -->
          <div class="space-y-4">
            <!-- Loading State -->
            <div v-if="activityStore.isLoading && activityStore.activities.length === 0" class="space-y-4">
              <div v-for="i in 5" :key="i" class="animate-pulse flex space-x-4 p-4 bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700">
                <div class="rounded-full bg-gray-200 dark:bg-gray-700 h-10 w-10"></div>
                <div class="flex-1 space-y-2 py-1">
                  <div class="h-4 bg-gray-200 dark:bg-gray-700 rounded w-3/4"></div>
                  <div class="h-3 bg-gray-200 dark:bg-gray-700 rounded w-1/2"></div>
                </div>
              </div>
            </div>

            <!-- Empty State -->
            <div v-else-if="activityStore.activities.length === 0" class="text-center py-12 bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700">
              <ClockIcon class="mx-auto h-12 w-12 text-gray-400" />
              <h3 class="mt-4 text-lg font-medium text-gray-900 dark:text-white">No activity yet</h3>
              <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">
                Activity from your workspaces will appear here.
              </p>
            </div>

            <!-- Activity Items -->
            <div
              v-for="activity in activityStore.activities"
              :key="activity.id"
              class="flex gap-4 p-4 bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700 hover:border-aegis-300 dark:hover:border-aegis-600 transition-colors"
            >
              <!-- Icon -->
              <div
                class="flex-shrink-0 w-10 h-10 rounded-full flex items-center justify-center"
                :class="getColorClasses(activity)"
              >
                <component :is="getActivityIcon(activity)" class="h-5 w-5" />
              </div>

              <!-- Content -->
              <div class="flex-1 min-w-0">
                <div class="flex items-start justify-between">
                  <div>
                    <p class="text-sm text-gray-900 dark:text-white">
                      <span class="font-medium">{{ activity.actorName }}</span>
                      <span class="text-gray-600 dark:text-gray-400">
                        {{ ' ' }}{{ activity.description }}
                      </span>
                    </p>

                    <!-- Resource Link -->
                    <div v-if="activity.resourceName" class="mt-1 flex items-center gap-2 text-sm">
                      <span class="text-gray-500 dark:text-gray-400">in</span>
                      <RouterLink
                        v-if="activity.resourceType === 'Session'"
                        :to="`/chat?session=${activity.resourceId}`"
                        class="text-aegis-600 dark:text-aegis-400 hover:underline"
                      >
                        {{ activity.resourceName }}
                      </RouterLink>
                      <RouterLink
                        v-else-if="activity.resourceType === 'Workspace'"
                        :to="`/workspaces/${activity.resourceId}`"
                        class="text-aegis-600 dark:text-aegis-400 hover:underline"
                      >
                        {{ activity.resourceName }}
                      </RouterLink>
                      <span v-else class="text-aegis-600 dark:text-aegis-400">
                        {{ activity.resourceName }}
                      </span>
                    </div>

                    <!-- Workspace -->
                    <div v-if="activity.workspaceName" class="mt-1 flex items-center gap-1 text-xs text-gray-500 dark:text-gray-400">
                      <FolderIcon class="h-3 w-3" />
                      {{ activity.workspaceName }}
                    </div>
                  </div>

                  <!-- Time -->
                  <span class="flex-shrink-0 text-xs text-gray-500 dark:text-gray-400">
                    {{ formatTime(activity.occurredAt) }}
                  </span>
                </div>
              </div>
            </div>

            <!-- Load More -->
            <div v-if="activityStore.hasMore" class="flex justify-center pt-4">
              <button
                class="btn-secondary"
                :disabled="activityStore.isLoading"
                @click="loadMore"
              >
                <ArrowPathIcon v-if="activityStore.isLoading" class="h-4 w-4 mr-2 animate-spin" />
                Load more
              </button>
            </div>
          </div>
        </TabPanel>
      </TabPanels>
    </TabGroup>
  </div>
</template>
