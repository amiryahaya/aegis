<script setup lang="ts">
import { ref, computed } from 'vue'
import { Menu, MenuButton, MenuItem, MenuItems } from '@headlessui/vue'
import {
  Bars3Icon,
  MoonIcon,
  SunIcon,
  UserCircleIcon,
  ArrowRightOnRectangleIcon,
  MagnifyingGlassIcon,
  Cog6ToothIcon,
  QuestionMarkCircleIcon,
  CommandLineIcon
} from '@heroicons/vue/24/outline'
import { useAuthStore } from '@/stores/auth'
import { useRouter } from 'vue-router'
import { useCommandPalette } from '@/composables/useCommandPalette'
import NotificationBell from '@/components/notifications/NotificationBell.vue'
import ConnectionStatus from '@/components/connection/ConnectionStatus.vue'

const emit = defineEmits<{
  toggleSidebar: []
}>()

const authStore = useAuthStore()
const router = useRouter()
const { open: openCommandPalette } = useCommandPalette()
const isDark = ref(document.documentElement.classList.contains('dark'))
const isMac = computed(() => navigator.platform.toUpperCase().indexOf('MAC') >= 0)
const searchQuery = ref('')

function handleSearch() {
  if (searchQuery.value.trim()) {
    router.push({ path: '/search', query: { q: searchQuery.value.trim() } })
    searchQuery.value = ''
  }
}

function navigateToSearch() {
  router.push('/search')
}

function toggleDarkMode() {
  isDark.value = !isDark.value
  document.documentElement.classList.toggle('dark', isDark.value)
  localStorage.setItem('theme', isDark.value ? 'dark' : 'light')
}

function logout() {
  authStore.logout()
  router.push('/login')
}
</script>

<template>
  <header class="sticky top-0 z-10 flex h-16 shrink-0 items-center gap-4 border-b border-gray-200 bg-white px-4 dark:border-gray-700 dark:bg-gray-800">
    <!-- Mobile menu button -->
    <button
      type="button"
      class="btn-ghost p-2 lg:hidden"
      @click="emit('toggleSidebar')"
    >
      <Bars3Icon class="h-6 w-6" />
    </button>

    <!-- Desktop sidebar toggle -->
    <button
      type="button"
      class="btn-ghost hidden p-2 lg:block"
      @click="emit('toggleSidebar')"
    >
      <Bars3Icon class="h-5 w-5" />
    </button>

    <!-- Search Bar + Command Palette Trigger -->
    <div class="hidden sm:flex flex-1 max-w-lg mx-4 gap-2" data-tour="search">
      <form @submit.prevent="handleSearch" class="relative flex-1">
        <MagnifyingGlassIcon class="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
        <input
          v-model="searchQuery"
          type="text"
          placeholder="Search..."
          class="w-full pl-9 pr-4 py-1.5 text-sm border border-gray-300 dark:border-gray-600 rounded-lg bg-gray-50 dark:bg-gray-700 text-gray-900 dark:text-gray-100 placeholder-gray-500 dark:placeholder-gray-400 focus:ring-2 focus:ring-aegis-500 focus:border-transparent focus:bg-white dark:focus:bg-gray-600"
        />
      </form>

      <!-- Command Palette Button -->
      <button
        type="button"
        @click="openCommandPalette"
        data-tour="command-palette"
        class="flex items-center gap-2 px-3 py-1.5 text-sm text-gray-500 dark:text-gray-400 border border-gray-300 dark:border-gray-600 rounded-lg bg-gray-50 dark:bg-gray-700 hover:bg-gray-100 dark:hover:bg-gray-600 hover:text-gray-700 dark:hover:text-gray-200 transition-colors"
        title="Open command palette"
      >
        <CommandLineIcon class="h-4 w-4" />
        <span class="hidden lg:inline text-xs">Commands</span>
        <kbd class="hidden lg:inline-flex items-center gap-0.5 px-1.5 py-0.5 text-xs font-mono bg-gray-200 dark:bg-gray-600 rounded">
          <span>{{ isMac ? '⌘' : 'Ctrl' }}</span>
          <span>K</span>
        </kbd>
      </button>
    </div>

    <!-- Mobile search button -->
    <button
      type="button"
      class="btn-ghost p-2 sm:hidden"
      @click="navigateToSearch"
    >
      <MagnifyingGlassIcon class="h-5 w-5" />
    </button>

    <!-- Spacer (for mobile) -->
    <div class="flex-1 sm:hidden" />

    <!-- Right side actions -->
    <div class="flex items-center gap-2">
      <!-- Connection status -->
      <ConnectionStatus />

      <!-- Dark mode toggle -->
      <button
        type="button"
        class="btn-ghost p-2"
        data-tour="theme"
        @click="toggleDarkMode"
      >
        <MoonIcon v-if="!isDark" class="h-5 w-5" />
        <SunIcon v-else class="h-5 w-5" />
      </button>

      <!-- Notifications -->
      <div data-tour="notifications">
        <NotificationBell />
      </div>

      <!-- User menu -->
      <Menu as="div" class="relative">
        <MenuButton class="flex items-center gap-2 rounded-lg p-2 hover:bg-gray-100 dark:hover:bg-gray-700">
          <UserCircleIcon class="h-8 w-8 text-gray-400" />
          <span class="hidden text-sm font-medium text-gray-700 dark:text-gray-200 sm:block">
            {{ authStore.userName }}
          </span>
        </MenuButton>

        <transition
          enter-active-class="transition ease-out duration-100"
          enter-from-class="transform opacity-0 scale-95"
          enter-to-class="transform opacity-100 scale-100"
          leave-active-class="transition ease-in duration-75"
          leave-from-class="transform opacity-100 scale-100"
          leave-to-class="transform opacity-0 scale-95"
        >
          <MenuItems class="absolute right-0 mt-2 w-48 origin-top-right rounded-lg bg-white py-1 shadow-lg ring-1 ring-black ring-opacity-5 focus:outline-none dark:bg-gray-800 dark:ring-gray-700">
            <MenuItem v-slot="{ active }">
              <RouterLink
                to="/profile"
                class="flex w-full items-center gap-2 px-4 py-2 text-sm"
                :class="active ? 'bg-gray-100 dark:bg-gray-700' : ''"
              >
                <UserCircleIcon class="h-5 w-5" />
                Profile
              </RouterLink>
            </MenuItem>
            <MenuItem v-slot="{ active }">
              <RouterLink
                to="/settings"
                class="flex w-full items-center gap-2 px-4 py-2 text-sm"
                :class="active ? 'bg-gray-100 dark:bg-gray-700' : ''"
              >
                <Cog6ToothIcon class="h-5 w-5" />
                Settings
              </RouterLink>
            </MenuItem>
            <MenuItem v-slot="{ active }">
              <RouterLink
                to="/help"
                class="flex w-full items-center gap-2 px-4 py-2 text-sm"
                :class="active ? 'bg-gray-100 dark:bg-gray-700' : ''"
              >
                <QuestionMarkCircleIcon class="h-5 w-5" />
                Help
              </RouterLink>
            </MenuItem>
            <div class="border-t border-gray-200 dark:border-gray-700 my-1" />
            <MenuItem v-slot="{ active }">
              <button
                class="flex w-full items-center gap-2 px-4 py-2 text-sm text-red-600 dark:text-red-400"
                :class="active ? 'bg-gray-100 dark:bg-gray-700' : ''"
                @click="logout"
              >
                <ArrowRightOnRectangleIcon class="h-5 w-5" />
                Sign out
              </button>
            </MenuItem>
          </MenuItems>
        </transition>
      </Menu>
    </div>
  </header>
</template>
