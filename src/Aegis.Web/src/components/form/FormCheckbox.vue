<script setup lang="ts">
import { computed } from 'vue'

interface Props {
  label: string
  name: string
  disabled?: boolean
  description?: string
  error?: string
  touched?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  disabled: false
})

const modelValue = defineModel<boolean>()

const hasError = computed(() => props.touched && props.error)
</script>

<template>
  <div class="relative flex items-start">
    <div class="flex h-6 items-center">
      <input
        :id="name"
        :name="name"
        type="checkbox"
        :disabled="disabled"
        :aria-describedby="description ? `${name}-description` : undefined"
        :aria-invalid="hasError ? 'true' : undefined"
        class="h-4 w-4 rounded border-gray-300 dark:border-gray-600 text-blue-600 focus:ring-blue-600 dark:focus:ring-blue-500 bg-white dark:bg-gray-800 transition-colors duration-200"
        :class="{ 'border-red-300 dark:border-red-700': hasError }"
        v-model="modelValue"
      />
    </div>
    <div class="ml-3 text-sm leading-6">
      <label
        :for="name"
        class="font-medium text-gray-900 dark:text-gray-100"
        :class="{ 'text-red-600 dark:text-red-400': hasError }"
      >
        {{ label }}
      </label>
      <p
        v-if="description"
        :id="`${name}-description`"
        class="text-gray-500 dark:text-gray-400"
      >
        {{ description }}
      </p>
      <p
        v-if="hasError"
        class="text-red-600 dark:text-red-400"
        role="alert"
      >
        {{ error }}
      </p>
    </div>
  </div>
</template>
