<script setup lang="ts">
interface Props {
  targetId?: string
  label?: string
}

withDefaults(defineProps<Props>(), {
  targetId: 'main-content',
  label: 'Skip to main content'
})

const handleClick = (targetId: string) => {
  const target = document.getElementById(targetId)
  if (target) {
    target.focus()
    target.scrollIntoView({ behavior: 'smooth' })
  }
}
</script>

<template>
  <a
    :href="`#${targetId}`"
    @click.prevent="handleClick(targetId)"
    class="sr-only focus:not-sr-only focus:absolute focus:top-4 focus:left-4 focus:z-[100] focus:px-4 focus:py-2 focus:bg-blue-600 focus:text-white focus:rounded-md focus:shadow-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
  >
    {{ label }}
  </a>
</template>
