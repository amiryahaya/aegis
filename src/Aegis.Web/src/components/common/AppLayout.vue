<script setup lang="ts">
import { ref } from 'vue'
import AppHeader from './AppHeader.vue'
import AppSidebar from './AppSidebar.vue'

const sidebarOpen = ref(true)

function toggleSidebar() {
  sidebarOpen.value = !sidebarOpen.value
}
</script>

<template>
  <div class="flex h-screen bg-gray-50 dark:bg-gray-900">
    <!-- Sidebar -->
    <AppSidebar :open="sidebarOpen" @close="sidebarOpen = false" />

    <!-- Main content area -->
    <div class="flex flex-1 flex-col overflow-hidden">
      <!-- Header -->
      <AppHeader @toggle-sidebar="toggleSidebar" />

      <!-- Page content -->
      <main class="flex-1 overflow-auto">
        <slot />
      </main>
    </div>
  </div>
</template>
