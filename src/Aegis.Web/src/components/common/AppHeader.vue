<script setup lang="ts">
import { ref } from 'vue'
import { Menu, MenuButton, MenuItem, MenuItems } from '@headlessui/vue'
import {
  Bars3Icon,
  BellIcon,
  MoonIcon,
  SunIcon,
  UserCircleIcon,
  ArrowRightOnRectangleIcon
} from '@heroicons/vue/24/outline'
import { useAuthStore } from '@/stores/auth'
import { useRouter } from 'vue-router'

const emit = defineEmits<{
  toggleSidebar: []
}>()

const authStore = useAuthStore()
const router = useRouter()
const isDark = ref(document.documentElement.classList.contains('dark'))

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

    <!-- Spacer -->
    <div class="flex-1" />

    <!-- Right side actions -->
    <div class="flex items-center gap-2">
      <!-- Dark mode toggle -->
      <button
        type="button"
        class="btn-ghost p-2"
        @click="toggleDarkMode"
      >
        <MoonIcon v-if="!isDark" class="h-5 w-5" />
        <SunIcon v-else class="h-5 w-5" />
      </button>

      <!-- Notifications -->
      <button type="button" class="btn-ghost relative p-2">
        <BellIcon class="h-5 w-5" />
        <span class="absolute right-1 top-1 h-2 w-2 rounded-full bg-red-500" />
      </button>

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
              <button
                class="flex w-full items-center gap-2 px-4 py-2 text-sm"
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
