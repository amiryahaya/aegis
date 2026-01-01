<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import AppLayout from '@/components/common/AppLayout.vue'

const route = useRoute()
const isAuthPage = computed(() => route.meta.requiresAuth === false)
</script>

<template>
  <div class="min-h-screen">
    <component :is="isAuthPage ? 'div' : AppLayout">
      <RouterView />
    </component>
  </div>
</template>
