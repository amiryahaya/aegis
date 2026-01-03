<script setup lang="ts">
import SkeletonLoader from '@/components/common/SkeletonLoader.vue'
</script>

<template>
  <div class="card p-4">
    <!-- Header with title and status -->
    <div class="mb-2 flex items-start justify-between">
      <SkeletonLoader type="text" width="60%" height="20px" />
      <SkeletonLoader type="rect" width="60px" height="20px" rounded="full" />
    </div>

    <!-- Description -->
    <div class="mb-3 space-y-1">
      <SkeletonLoader type="text" width="100%" height="14px" />
      <SkeletonLoader type="text" width="75%" height="14px" />
    </div>

    <!-- Meta info -->
    <div class="flex items-center gap-2">
      <SkeletonLoader type="rect" width="70px" height="20px" rounded="full" />
      <SkeletonLoader type="text" width="60px" height="12px" />
      <SkeletonLoader type="text" width="100px" height="12px" />
    </div>

    <!-- Tags -->
    <div class="mt-2 flex gap-1">
      <SkeletonLoader type="rect" width="50px" height="18px" rounded="sm" />
      <SkeletonLoader type="rect" width="40px" height="18px" rounded="sm" />
      <SkeletonLoader type="rect" width="55px" height="18px" rounded="sm" />
    </div>
  </div>
</template>
