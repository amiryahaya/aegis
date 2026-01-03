<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { Listbox, ListboxButton, ListboxOptions, ListboxOption } from '@headlessui/vue'
import { CalendarIcon, ChevronDownIcon } from '@heroicons/vue/24/outline'
import type { DateRangePreset, DateRange } from '@/types/analytics'
import { getDateRangeFromPreset, formatDateRange } from '@/types/analytics'

const props = withDefaults(defineProps<{
  modelValue: DateRange
  showCustom?: boolean
}>(), {
  showCustom: true
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: DateRange): void
}>()

const presets: { value: DateRangePreset; label: string }[] = [
  { value: 'today', label: 'Today' },
  { value: 'yesterday', label: 'Yesterday' },
  { value: 'last7days', label: 'Last 7 Days' },
  { value: 'last30days', label: 'Last 30 Days' },
  { value: 'last90days', label: 'Last 90 Days' },
  { value: 'thisMonth', label: 'This Month' },
  { value: 'lastMonth', label: 'Last Month' },
  { value: 'thisYear', label: 'This Year' }
]

if (props.showCustom) {
  presets.push({ value: 'custom', label: 'Custom Range' })
}

const selectedPreset = ref<DateRangePreset>(props.modelValue.preset || 'last30days')
const showCustomInputs = ref(false)
const customStart = ref('')
const customEnd = ref('')

const selectedLabel = computed(() => {
  if (selectedPreset.value === 'custom') {
    return formatDateRange(props.modelValue)
  }
  return presets.find(p => p.value === selectedPreset.value)?.label || 'Select range'
})

watch(selectedPreset, (preset) => {
  if (preset === 'custom') {
    showCustomInputs.value = true
    customStart.value = props.modelValue.start.toISOString().split('T')[0]
    customEnd.value = props.modelValue.end.toISOString().split('T')[0]
  } else {
    showCustomInputs.value = false
    emit('update:modelValue', getDateRangeFromPreset(preset))
  }
})

function applyCustomRange() {
  if (customStart.value && customEnd.value) {
    emit('update:modelValue', {
      start: new Date(customStart.value),
      end: new Date(customEnd.value),
      preset: 'custom'
    })
    showCustomInputs.value = false
  }
}
</script>

<template>
  <div class="relative">
    <Listbox v-model="selectedPreset">
      <div class="relative">
        <ListboxButton
          class="relative w-full cursor-pointer rounded-lg bg-white py-2 pl-3 pr-10 text-left shadow-sm ring-1 ring-inset ring-gray-300 focus:outline-none focus:ring-2 focus:ring-aegis-500 dark:bg-gray-800 dark:ring-gray-600 sm:text-sm"
        >
          <span class="flex items-center gap-2">
            <CalendarIcon class="h-4 w-4 text-gray-400" />
            <span class="block truncate text-gray-900 dark:text-white">
              {{ selectedLabel }}
            </span>
          </span>
          <span class="pointer-events-none absolute inset-y-0 right-0 flex items-center pr-2">
            <ChevronDownIcon class="h-4 w-4 text-gray-400" />
          </span>
        </ListboxButton>

        <transition
          leave-active-class="transition duration-100 ease-in"
          leave-from-class="opacity-100"
          leave-to-class="opacity-0"
        >
          <ListboxOptions
            class="absolute z-10 mt-1 max-h-60 w-full overflow-auto rounded-lg bg-white py-1 text-base shadow-lg ring-1 ring-black/5 focus:outline-none dark:bg-gray-800 sm:text-sm"
          >
            <ListboxOption
              v-for="preset in presets"
              :key="preset.value"
              v-slot="{ active, selected }"
              :value="preset.value"
            >
              <li
                class="relative cursor-pointer select-none py-2 pl-10 pr-4"
                :class="{
                  'bg-aegis-100 text-aegis-900 dark:bg-aegis-900/30 dark:text-aegis-100': active,
                  'text-gray-900 dark:text-gray-100': !active
                }"
              >
                <span
                  class="block truncate"
                  :class="{ 'font-medium': selected, 'font-normal': !selected }"
                >
                  {{ preset.label }}
                </span>
                <span
                  v-if="selected"
                  class="absolute inset-y-0 left-0 flex items-center pl-3 text-aegis-600 dark:text-aegis-400"
                >
                  <svg class="h-4 w-4" viewBox="0 0 20 20" fill="currentColor">
                    <path fill-rule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clip-rule="evenodd" />
                  </svg>
                </span>
              </li>
            </ListboxOption>
          </ListboxOptions>
        </transition>
      </div>
    </Listbox>

    <!-- Custom date inputs -->
    <div
      v-if="showCustomInputs"
      class="mt-2 rounded-lg border border-gray-200 bg-white p-3 shadow-sm dark:border-gray-700 dark:bg-gray-800"
    >
      <div class="grid grid-cols-2 gap-3">
        <div>
          <label class="block text-xs font-medium text-gray-500 dark:text-gray-400">
            Start Date
          </label>
          <input
            v-model="customStart"
            type="date"
            class="mt-1 block w-full rounded-md border-gray-300 text-sm shadow-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
          />
        </div>
        <div>
          <label class="block text-xs font-medium text-gray-500 dark:text-gray-400">
            End Date
          </label>
          <input
            v-model="customEnd"
            type="date"
            class="mt-1 block w-full rounded-md border-gray-300 text-sm shadow-sm focus:border-aegis-500 focus:ring-aegis-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
          />
        </div>
      </div>
      <button
        class="mt-3 w-full rounded-md bg-aegis-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-aegis-700"
        @click="applyCustomRange"
      >
        Apply Range
      </button>
    </div>
  </div>
</template>
