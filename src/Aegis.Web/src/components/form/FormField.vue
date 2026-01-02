<script setup lang="ts">
import { computed } from 'vue'
import { ExclamationCircleIcon, CheckCircleIcon } from '@heroicons/vue/20/solid'

interface Props {
  label?: string
  name: string
  type?: 'text' | 'email' | 'password' | 'number' | 'textarea' | 'select'
  placeholder?: string
  disabled?: boolean
  required?: boolean
  error?: string
  touched?: boolean
  hint?: string
  rows?: number
  options?: Array<{ value: string; label: string }>
  showSuccessIcon?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  type: 'text',
  disabled: false,
  required: false,
  rows: 3,
  showSuccessIcon: false
})

const modelValue = defineModel<string | number>()

const hasError = computed(() => props.touched && props.error)
const isValid = computed(() => props.touched && !props.error && modelValue.value)

const inputClasses = computed(() => {
  const base = 'block w-full rounded-md border-0 py-2 px-3 shadow-sm ring-1 ring-inset focus:ring-2 focus:ring-inset sm:text-sm sm:leading-6 transition-colors duration-200'

  if (hasError.value) {
    return `${base} text-red-900 dark:text-red-200 ring-red-300 dark:ring-red-700 placeholder:text-red-300 focus:ring-red-500 bg-red-50 dark:bg-red-900/20`
  }

  if (isValid.value && props.showSuccessIcon) {
    return `${base} text-green-900 dark:text-green-200 ring-green-300 dark:ring-green-700 focus:ring-green-500 bg-green-50 dark:bg-green-900/20`
  }

  return `${base} text-gray-900 dark:text-gray-100 ring-gray-300 dark:ring-gray-600 placeholder:text-gray-400 focus:ring-blue-600 dark:focus:ring-blue-500 bg-white dark:bg-gray-800`
})

const labelClasses = computed(() => {
  const base = 'block text-sm font-medium leading-6'
  return hasError.value
    ? `${base} text-red-600 dark:text-red-400`
    : `${base} text-gray-900 dark:text-gray-100`
})
</script>

<template>
  <div class="space-y-1">
    <!-- Label -->
    <label v-if="label" :for="name" :class="labelClasses">
      {{ label }}
      <span v-if="required" class="text-red-500 ml-0.5">*</span>
    </label>

    <!-- Input wrapper -->
    <div class="relative">
      <!-- Text Input -->
      <input
        v-if="type !== 'textarea' && type !== 'select'"
        :id="name"
        :type="type"
        :name="name"
        :placeholder="placeholder"
        :disabled="disabled"
        :required="required"
        :class="inputClasses"
        :aria-invalid="hasError ? 'true' : undefined"
        :aria-describedby="hasError ? `${name}-error` : hint ? `${name}-hint` : undefined"
        v-model="modelValue"
      />

      <!-- Textarea -->
      <textarea
        v-else-if="type === 'textarea'"
        :id="name"
        :name="name"
        :placeholder="placeholder"
        :disabled="disabled"
        :required="required"
        :rows="rows"
        :class="inputClasses"
        :aria-invalid="hasError ? 'true' : undefined"
        :aria-describedby="hasError ? `${name}-error` : hint ? `${name}-hint` : undefined"
        v-model="modelValue"
      />

      <!-- Select -->
      <select
        v-else-if="type === 'select'"
        :id="name"
        :name="name"
        :disabled="disabled"
        :required="required"
        :class="inputClasses"
        :aria-invalid="hasError ? 'true' : undefined"
        :aria-describedby="hasError ? `${name}-error` : hint ? `${name}-hint` : undefined"
        v-model="modelValue"
      >
        <option v-if="placeholder" value="" disabled>{{ placeholder }}</option>
        <option
          v-for="option in options"
          :key="option.value"
          :value="option.value"
        >
          {{ option.label }}
        </option>
      </select>

      <!-- Status icons -->
      <div
        v-if="hasError || (isValid && showSuccessIcon)"
        class="pointer-events-none absolute inset-y-0 right-0 flex items-center pr-3"
      >
        <ExclamationCircleIcon
          v-if="hasError"
          class="h-5 w-5 text-red-500"
          aria-hidden="true"
        />
        <CheckCircleIcon
          v-else-if="isValid && showSuccessIcon"
          class="h-5 w-5 text-green-500"
          aria-hidden="true"
        />
      </div>
    </div>

    <!-- Error message -->
    <p
      v-if="hasError"
      :id="`${name}-error`"
      class="text-sm text-red-600 dark:text-red-400"
      role="alert"
    >
      {{ error }}
    </p>

    <!-- Hint text -->
    <p
      v-else-if="hint"
      :id="`${name}-hint`"
      class="text-sm text-gray-500 dark:text-gray-400"
    >
      {{ hint }}
    </p>
  </div>
</template>
