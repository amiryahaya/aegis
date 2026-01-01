<script setup lang="ts">
import { ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { ExclamationCircleIcon } from '@heroicons/vue/24/outline'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()

const email = ref('')
const password = ref('')
const loading = ref(false)
const error = ref('')

async function handleSubmit() {
  if (!email.value || !password.value) {
    error.value = 'Please enter both email and password'
    return
  }

  loading.value = true
  error.value = ''

  const success = await authStore.login({
    email: email.value,
    password: password.value
  })

  loading.value = false

  if (success) {
    const redirect = route.query.redirect as string || '/'
    router.push(redirect)
  } else {
    error.value = authStore.error || 'Login failed. Please try again.'
  }
}
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-gradient-to-br from-aegis-50 to-gray-100 px-4 py-12 dark:from-gray-900 dark:to-gray-800">
    <div class="w-full max-w-md">
      <!-- Logo and title -->
      <div class="mb-8 text-center">
        <h1 class="text-4xl font-bold text-aegis-600 dark:text-aegis-400">AEGIS</h1>
        <p class="mt-2 text-gray-600 dark:text-gray-400">
          Agentic Entity & Graph Intelligence System
        </p>
      </div>

      <!-- Login card -->
      <div class="card p-8">
        <h2 class="mb-6 text-center text-xl font-semibold text-gray-900 dark:text-white">
          Sign in to your account
        </h2>

        <!-- Error message -->
        <div
          v-if="error"
          class="mb-4 flex items-center gap-2 rounded-lg bg-red-50 p-3 text-sm text-red-600 dark:bg-red-900/30 dark:text-red-400"
        >
          <ExclamationCircleIcon class="h-5 w-5 shrink-0" />
          {{ error }}
        </div>

        <form @submit.prevent="handleSubmit" class="space-y-4">
          <!-- Email -->
          <div>
            <label for="email" class="block text-sm font-medium text-gray-700 dark:text-gray-300">
              Email address
            </label>
            <input
              id="email"
              v-model="email"
              type="email"
              autocomplete="email"
              required
              class="input mt-1"
              placeholder="you@example.com"
            />
          </div>

          <!-- Password -->
          <div>
            <label for="password" class="block text-sm font-medium text-gray-700 dark:text-gray-300">
              Password
            </label>
            <input
              id="password"
              v-model="password"
              type="password"
              autocomplete="current-password"
              required
              class="input mt-1"
              placeholder="••••••••"
            />
          </div>

          <!-- Submit button -->
          <button
            type="submit"
            :disabled="loading"
            class="btn-primary w-full py-2.5"
          >
            <template v-if="loading">
              <svg class="mr-2 h-4 w-4 animate-spin" viewBox="0 0 24 24">
                <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4" fill="none" />
                <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
              </svg>
              Signing in...
            </template>
            <template v-else>
              Sign in
            </template>
          </button>
        </form>

        <!-- Demo credentials hint -->
        <div class="mt-6 rounded-lg bg-gray-50 p-4 dark:bg-gray-700/50">
          <p class="text-xs text-gray-500 dark:text-gray-400">
            <strong>Demo credentials:</strong><br />
            Email: demo@aegis.local<br />
            Password: demo123
          </p>
        </div>
      </div>
    </div>
  </div>
</template>
