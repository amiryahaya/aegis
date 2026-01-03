<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import {
  ExclamationTriangleIcon,
  HomeIcon,
  ArrowLeftIcon,
  MagnifyingGlassIcon
} from '@heroicons/vue/24/outline'

const router = useRouter()
const authStore = useAuthStore()

function goBack() {
  router.back()
}

function goHome() {
  router.push(authStore.isAuthenticated ? '/' : '/login')
}
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-gradient-to-br from-aegis-50 to-gray-100 px-4 py-12 dark:from-gray-900 dark:to-gray-800">
    <div class="w-full max-w-lg text-center">
      <!-- 404 illustration -->
      <div class="mb-8">
        <div class="mx-auto flex h-24 w-24 items-center justify-center rounded-full bg-amber-100 dark:bg-amber-900/30">
          <ExclamationTriangleIcon class="h-12 w-12 text-amber-600 dark:text-amber-400" />
        </div>
      </div>

      <!-- Error message -->
      <h1 class="text-6xl font-bold text-gray-900 dark:text-white">404</h1>
      <h2 class="mt-4 text-2xl font-semibold text-gray-900 dark:text-white">
        Page not found
      </h2>
      <p class="mt-2 text-gray-600 dark:text-gray-400">
        Sorry, we couldn't find the page you're looking for.
        It might have been moved, deleted, or never existed.
      </p>

      <!-- Actions -->
      <div class="mt-8 flex flex-col gap-3 sm:flex-row sm:justify-center">
        <button
          @click="goBack"
          class="btn-ghost inline-flex items-center justify-center gap-2 px-6 py-2.5"
        >
          <ArrowLeftIcon class="h-4 w-4" />
          Go back
        </button>
        <button
          @click="goHome"
          class="btn-primary inline-flex items-center justify-center gap-2 px-6 py-2.5"
        >
          <HomeIcon class="h-4 w-4" />
          {{ authStore.isAuthenticated ? 'Go to Dashboard' : 'Go to Login' }}
        </button>
      </div>

      <!-- Search suggestion -->
      <div class="mt-12 rounded-xl bg-white p-6 shadow-sm dark:bg-gray-800">
        <h3 class="font-medium text-gray-900 dark:text-white">
          Looking for something specific?
        </h3>
        <p class="mt-1 text-sm text-gray-600 dark:text-gray-400">
          Try using the search feature to find what you need.
        </p>
        <RouterLink
          v-if="authStore.isAuthenticated"
          to="/search"
          class="mt-4 inline-flex items-center gap-2 text-sm font-medium text-aegis-600 hover:text-aegis-500 dark:text-aegis-400"
        >
          <MagnifyingGlassIcon class="h-4 w-4" />
          Search AEGIS
        </RouterLink>
      </div>

      <!-- Quick links -->
      <div class="mt-8">
        <p class="text-sm text-gray-500 dark:text-gray-400">
          Helpful links:
        </p>
        <div class="mt-2 flex flex-wrap justify-center gap-4">
          <RouterLink
            v-if="authStore.isAuthenticated"
            to="/chat"
            class="text-sm font-medium text-aegis-600 hover:text-aegis-500 dark:text-aegis-400"
          >
            Chat
          </RouterLink>
          <RouterLink
            v-if="authStore.isAuthenticated"
            to="/sessions"
            class="text-sm font-medium text-aegis-600 hover:text-aegis-500 dark:text-aegis-400"
          >
            Sessions
          </RouterLink>
          <RouterLink
            v-if="authStore.isAuthenticated"
            to="/workspaces"
            class="text-sm font-medium text-aegis-600 hover:text-aegis-500 dark:text-aegis-400"
          >
            Workspaces
          </RouterLink>
          <RouterLink
            v-if="authStore.isAuthenticated"
            to="/help"
            class="text-sm font-medium text-aegis-600 hover:text-aegis-500 dark:text-aegis-400"
          >
            Help Center
          </RouterLink>
          <RouterLink
            v-if="!authStore.isAuthenticated"
            to="/login"
            class="text-sm font-medium text-aegis-600 hover:text-aegis-500 dark:text-aegis-400"
          >
            Sign In
          </RouterLink>
          <RouterLink
            v-if="!authStore.isAuthenticated"
            to="/register"
            class="text-sm font-medium text-aegis-600 hover:text-aegis-500 dark:text-aegis-400"
          >
            Create Account
          </RouterLink>
        </div>
      </div>
    </div>
  </div>
</template>
