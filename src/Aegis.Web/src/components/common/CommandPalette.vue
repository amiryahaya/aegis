<script setup lang="ts">
import { ref, watch, nextTick } from 'vue'
import {
  Dialog,
  DialogPanel,
  TransitionRoot,
  TransitionChild
} from '@headlessui/vue'
import {
  MagnifyingGlassIcon,
  HomeIcon,
  ChatBubbleLeftRightIcon,
  ClockIcon,
  FolderIcon,
  BellIcon,
  UserIcon,
  Cog6ToothIcon,
  ShieldCheckIcon,
  QuestionMarkCircleIcon,
  PlusIcon,
  FolderPlusIcon,
  MoonIcon,
  ArrowRightOnRectangleIcon,
  ArrowDownTrayIcon,
  CommandLineIcon,
  RssIcon
} from '@heroicons/vue/24/outline'
import { useCommandPalette } from '@/composables/useCommandPalette'
import type { Command } from '@/types/command'

const {
  isOpen,
  query,
  selectedIndex,
  groupedCommands,
  filteredCommands,
  close,
  setQuery,
  selectIndex,
  executeCommand
} = useCommandPalette()

const inputRef = ref<HTMLInputElement | null>(null)
const listRef = ref<HTMLDivElement | null>(null)

// Focus input when dialog opens
watch(isOpen, async (open) => {
  if (open) {
    await nextTick()
    inputRef.value?.focus()
  }
})

// Scroll selected item into view
watch(selectedIndex, async (index) => {
  await nextTick()
  const list = listRef.value
  if (!list) return

  const items = list.querySelectorAll('[data-command-item]')
  const selectedItem = items[index] as HTMLElement
  if (selectedItem) {
    selectedItem.scrollIntoView({ block: 'nearest' })
  }
})

// Get icon component by name
function getIcon(iconName?: string) {
  const icons: Record<string, typeof HomeIcon> = {
    HomeIcon,
    ChatBubbleLeftRightIcon,
    ClockIcon,
    FolderIcon,
    MagnifyingGlassIcon,
    BellIcon,
    RssIcon,
    UserIcon,
    Cog6ToothIcon,
    ShieldCheckIcon,
    QuestionMarkCircleIcon,
    PlusIcon,
    FolderPlusIcon,
    MoonIcon,
    ArrowRightOnRectangleIcon,
    ArrowDownTrayIcon,
    CommandLineIcon
  }
  return iconName ? icons[iconName] : null
}

// Check if command is disabled
function isDisabled(command: Command): boolean {
  return typeof command.disabled === 'function' ? command.disabled() : command.disabled || false
}

// Get flat index from grouped commands
function getFlatIndex(groupIndex: number, commandIndex: number): number {
  let flatIndex = 0
  for (let i = 0; i < groupIndex; i++) {
    flatIndex += groupedCommands.value[i].commands.length
  }
  return flatIndex + commandIndex
}

// Handle item click
function handleItemClick(command: Command) {
  if (!isDisabled(command)) {
    executeCommand(command)
  }
}

// Handle mouse enter for selection
function handleMouseEnter(groupIndex: number, commandIndex: number) {
  selectIndex(getFlatIndex(groupIndex, commandIndex))
}
</script>

<template>
  <TransitionRoot :show="isOpen" as="template">
    <Dialog
      :open="isOpen"
      @close="close"
      class="relative z-50"
    >
      <!-- Backdrop -->
      <TransitionChild
        as="template"
        enter="ease-out duration-200"
        enter-from="opacity-0"
        enter-to="opacity-100"
        leave="ease-in duration-150"
        leave-from="opacity-100"
        leave-to="opacity-0"
      >
        <div class="fixed inset-0 bg-gray-900/50 dark:bg-gray-900/80 backdrop-blur-sm" />
      </TransitionChild>

      <!-- Dialog content -->
      <div class="fixed inset-0 overflow-y-auto p-4 sm:p-6 md:p-20">
        <TransitionChild
          as="template"
          enter="ease-out duration-200"
          enter-from="opacity-0 scale-95"
          enter-to="opacity-100 scale-100"
          leave="ease-in duration-150"
          leave-from="opacity-100 scale-100"
          leave-to="opacity-0 scale-95"
        >
          <DialogPanel
            class="mx-auto max-w-xl transform overflow-hidden rounded-xl bg-white dark:bg-gray-800 shadow-2xl ring-1 ring-black/5 dark:ring-white/10 transition-all"
          >
            <!-- Search input -->
            <div class="relative">
              <MagnifyingGlassIcon
                class="pointer-events-none absolute left-4 top-3.5 h-5 w-5 text-gray-400 dark:text-gray-500"
              />
              <input
                ref="inputRef"
                type="text"
                :value="query"
                @input="setQuery(($event.target as HTMLInputElement).value)"
                class="h-12 w-full border-0 bg-transparent pl-11 pr-4 text-gray-900 dark:text-gray-100 placeholder:text-gray-400 dark:placeholder:text-gray-500 focus:ring-0 sm:text-sm"
                placeholder="Search commands..."
              />
              <div class="absolute right-3 top-2.5 flex items-center gap-1">
                <kbd class="hidden sm:inline-flex h-6 items-center gap-1 rounded border border-gray-200 dark:border-gray-600 bg-gray-50 dark:bg-gray-700 px-1.5 font-mono text-xs text-gray-500 dark:text-gray-400">
                  <span class="text-xs">esc</span>
                </kbd>
              </div>
            </div>

            <!-- Divider -->
            <div class="border-t border-gray-200 dark:border-gray-700" />

            <!-- Commands list -->
            <div
              ref="listRef"
              class="max-h-80 scroll-py-2 overflow-y-auto"
            >
              <!-- Empty state -->
              <div
                v-if="filteredCommands.length === 0"
                class="py-14 px-6 text-center sm:px-14"
              >
                <MagnifyingGlassIcon class="mx-auto h-6 w-6 text-gray-400 dark:text-gray-500" />
                <p class="mt-4 text-sm text-gray-900 dark:text-gray-100">
                  No commands found
                </p>
                <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">
                  Try a different search term
                </p>
              </div>

              <!-- Grouped commands -->
              <div v-else class="py-2">
                <div
                  v-for="(group, groupIndex) in groupedCommands"
                  :key="group.category"
                  class="mb-2"
                >
                  <!-- Group header -->
                  <div class="px-4 py-1.5 text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wider">
                    {{ group.label }}
                  </div>

                  <!-- Commands -->
                  <div
                    v-for="(command, commandIndex) in group.commands"
                    :key="command.id"
                    data-command-item
                    @click="handleItemClick(command)"
                    @mouseenter="handleMouseEnter(groupIndex, commandIndex)"
                    class="group flex cursor-pointer items-center gap-3 px-4 py-2 transition-colors"
                    :class="[
                      getFlatIndex(groupIndex, commandIndex) === selectedIndex
                        ? 'bg-aegis-50 dark:bg-aegis-900/30'
                        : 'hover:bg-gray-50 dark:hover:bg-gray-700/50',
                      isDisabled(command) ? 'opacity-50 cursor-not-allowed' : ''
                    ]"
                  >
                    <!-- Icon -->
                    <component
                      v-if="getIcon(command.icon)"
                      :is="getIcon(command.icon)"
                      class="h-5 w-5 flex-shrink-0"
                      :class="[
                        getFlatIndex(groupIndex, commandIndex) === selectedIndex
                          ? 'text-aegis-600 dark:text-aegis-400'
                          : 'text-gray-400 dark:text-gray-500'
                      ]"
                    />
                    <div v-else class="h-5 w-5 flex-shrink-0" />

                    <!-- Label and description -->
                    <div class="flex-1 min-w-0">
                      <div
                        class="text-sm font-medium truncate"
                        :class="[
                          getFlatIndex(groupIndex, commandIndex) === selectedIndex
                            ? 'text-aegis-900 dark:text-aegis-100'
                            : 'text-gray-900 dark:text-gray-100'
                        ]"
                      >
                        {{ command.label }}
                      </div>
                      <div
                        v-if="command.description"
                        class="text-xs truncate"
                        :class="[
                          getFlatIndex(groupIndex, commandIndex) === selectedIndex
                            ? 'text-aegis-600 dark:text-aegis-400'
                            : 'text-gray-500 dark:text-gray-400'
                        ]"
                      >
                        {{ command.description }}
                      </div>
                    </div>

                    <!-- Shortcut -->
                    <kbd
                      v-if="command.shortcut"
                      class="hidden sm:inline-flex items-center gap-1 rounded border px-1.5 py-0.5 font-mono text-xs"
                      :class="[
                        getFlatIndex(groupIndex, commandIndex) === selectedIndex
                          ? 'border-aegis-300 dark:border-aegis-700 bg-aegis-100 dark:bg-aegis-800/50 text-aegis-600 dark:text-aegis-400'
                          : 'border-gray-200 dark:border-gray-600 bg-gray-50 dark:bg-gray-700 text-gray-500 dark:text-gray-400'
                      ]"
                    >
                      {{ command.shortcut }}
                    </kbd>
                  </div>
                </div>
              </div>
            </div>

            <!-- Footer -->
            <div class="flex flex-wrap items-center bg-gray-50 dark:bg-gray-800/50 px-4 py-2.5 text-xs text-gray-500 dark:text-gray-400 border-t border-gray-200 dark:border-gray-700">
              <span class="flex items-center gap-1">
                <kbd class="inline-flex h-5 items-center rounded border border-gray-200 dark:border-gray-600 bg-white dark:bg-gray-700 px-1 font-mono">↑</kbd>
                <kbd class="inline-flex h-5 items-center rounded border border-gray-200 dark:border-gray-600 bg-white dark:bg-gray-700 px-1 font-mono">↓</kbd>
                <span class="ml-1">to navigate</span>
              </span>
              <span class="mx-2">·</span>
              <span class="flex items-center gap-1">
                <kbd class="inline-flex h-5 items-center rounded border border-gray-200 dark:border-gray-600 bg-white dark:bg-gray-700 px-1.5 font-mono">↵</kbd>
                <span class="ml-1">to select</span>
              </span>
              <span class="mx-2">·</span>
              <span class="flex items-center gap-1">
                <kbd class="inline-flex h-5 items-center rounded border border-gray-200 dark:border-gray-600 bg-white dark:bg-gray-700 px-1.5 font-mono">esc</kbd>
                <span class="ml-1">to close</span>
              </span>
            </div>
          </DialogPanel>
        </TransitionChild>
      </div>
    </Dialog>
  </TransitionRoot>
</template>
