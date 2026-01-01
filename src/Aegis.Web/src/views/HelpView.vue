<script setup lang="ts">
import { ref, computed } from 'vue'
import { TabGroup, TabList, Tab, TabPanels, TabPanel, Disclosure, DisclosureButton, DisclosurePanel } from '@headlessui/vue'
import {
  BookOpenIcon,
  CommandLineIcon,
  QuestionMarkCircleIcon,
  ChatBubbleLeftRightIcon,
  FolderIcon,
  MagnifyingGlassIcon,
  Cog6ToothIcon,
  RocketLaunchIcon,
  ChevronDownIcon,
  ArrowTopRightOnSquareIcon
} from '@heroicons/vue/24/outline'
import type { KeyboardShortcut, FAQ } from '@/types/profile'

const searchQuery = ref('')

const tabs = [
  { name: 'Getting Started', icon: RocketLaunchIcon },
  { name: 'Keyboard Shortcuts', icon: CommandLineIcon },
  { name: 'FAQ', icon: QuestionMarkCircleIcon }
]

const shortcuts: KeyboardShortcut[] = [
  // Navigation
  { key: 'g h', modifiers: [], description: 'Go to Dashboard', category: 'navigation', action: 'navigate:dashboard' },
  { key: 'g c', modifiers: [], description: 'Go to Chat', category: 'navigation', action: 'navigate:chat' },
  { key: 'g s', modifiers: [], description: 'Go to Sessions', category: 'navigation', action: 'navigate:sessions' },
  { key: 'g w', modifiers: [], description: 'Go to Workspaces', category: 'navigation', action: 'navigate:workspaces' },
  { key: 'g n', modifiers: [], description: 'Go to Notifications', category: 'navigation', action: 'navigate:notifications' },
  { key: 'g p', modifiers: [], description: 'Go to Profile', category: 'navigation', action: 'navigate:profile' },

  // Search
  { key: '/', modifiers: [], description: 'Focus search bar', category: 'search', action: 'focus:search' },
  { key: 'k', modifiers: ['ctrl'], description: 'Open command palette', category: 'search', action: 'open:command' },
  { key: 'Escape', modifiers: [], description: 'Clear search / Close modal', category: 'search', action: 'close' },

  // Chat
  { key: 'n', modifiers: [], description: 'New chat session', category: 'chat', action: 'chat:new' },
  { key: 'Enter', modifiers: ['ctrl'], description: 'Send message', category: 'chat', action: 'chat:send' },
  { key: 'ArrowUp', modifiers: [], description: 'Edit last message', category: 'chat', action: 'chat:edit' },

  // General
  { key: '?', modifiers: ['shift'], description: 'Show keyboard shortcuts', category: 'general', action: 'show:shortcuts' },
  { key: 't', modifiers: [], description: 'Toggle dark mode', category: 'general', action: 'toggle:theme' },
  { key: 'b', modifiers: [], description: 'Toggle sidebar', category: 'general', action: 'toggle:sidebar' }
]

const faqs: FAQ[] = [
  {
    id: '1',
    question: 'What is AEGIS?',
    answer: 'AEGIS (Agentic Entity & Graph Intelligence System) is a RAG (Retrieval-Augmented Generation) platform that allows you to query your documents and data sources using natural language. It combines semantic search with AI to provide accurate, context-aware responses.',
    category: 'general'
  },
  {
    id: '2',
    question: 'How do I create a new workspace?',
    answer: 'Navigate to Workspaces from the sidebar, then click the "Create Workspace" button. Give your workspace a name and description, then you can start adding documents and data sources to it.',
    category: 'workspaces'
  },
  {
    id: '3',
    question: 'What file types are supported for document upload?',
    answer: 'AEGIS supports a wide variety of document types including PDF, Word documents (.doc, .docx), Excel spreadsheets (.xls, .xlsx), PowerPoint presentations (.ppt, .pptx), plain text files (.txt), Markdown (.md), and HTML files.',
    category: 'documents'
  },
  {
    id: '4',
    question: 'How does semantic search work?',
    answer: 'When you upload documents, AEGIS processes them into smaller chunks and generates vector embeddings for each chunk. When you search, your query is also converted to a vector, and we find the most semantically similar document chunks using cosine similarity.',
    category: 'search'
  },
  {
    id: '5',
    question: 'What is a session?',
    answer: 'A session is a conversation thread with AEGIS. Each session maintains context from previous messages, allowing for follow-up questions and multi-turn conversations. You can create multiple sessions for different topics or projects.',
    category: 'chat'
  },
  {
    id: '6',
    question: 'How do I share a workspace with my team?',
    answer: 'Open the workspace you want to share, go to the Settings tab, and click "Share". You can invite team members by email and assign them different roles (Viewer, Commenter, Editor, or Admin).',
    category: 'workspaces'
  },
  {
    id: '7',
    question: 'Can I export my conversation history?',
    answer: 'Yes! You can export sessions in multiple formats including JSON, Markdown, HTML, and plain text. Go to Sessions, select a session, and click the export button to choose your preferred format.',
    category: 'chat'
  },
  {
    id: '8',
    question: 'How do I generate an API key?',
    answer: 'Go to your Profile, navigate to the API Keys tab, and click "Create Key". Give your key a name, select the scopes you need, and optionally set an expiration date. Make sure to copy the key immediately as it won\'t be shown again.',
    category: 'api'
  }
]

const gettingStartedSteps = [
  {
    title: 'Create a Workspace',
    description: 'Start by creating a workspace to organize your documents and data sources.',
    icon: FolderIcon,
    link: '/workspaces'
  },
  {
    title: 'Upload Documents',
    description: 'Add your documents (PDFs, Word, Excel, etc.) to your workspace for processing.',
    icon: BookOpenIcon,
    link: '/workspaces'
  },
  {
    title: 'Start a Chat',
    description: 'Create a new session and start asking questions about your documents.',
    icon: ChatBubbleLeftRightIcon,
    link: '/chat'
  },
  {
    title: 'Search & Explore',
    description: 'Use the global search to find information across all your workspaces.',
    icon: MagnifyingGlassIcon,
    link: '/search'
  },
  {
    title: 'Customize Settings',
    description: 'Configure your preferences, notifications, and theme.',
    icon: Cog6ToothIcon,
    link: '/settings'
  }
]

const shortcutsByCategory = computed(() => {
  const categories = ['navigation', 'search', 'chat', 'general'] as const
  return categories.map(category => ({
    name: category.charAt(0).toUpperCase() + category.slice(1),
    shortcuts: shortcuts.filter(s => s.category === category)
  }))
})

const filteredFaqs = computed(() => {
  if (!searchQuery.value.trim()) return faqs
  const query = searchQuery.value.toLowerCase()
  return faqs.filter(faq =>
    faq.question.toLowerCase().includes(query) ||
    faq.answer.toLowerCase().includes(query)
  )
})

function formatShortcut(shortcut: KeyboardShortcut): string {
  const modifiers = shortcut.modifiers.map(m => {
    switch (m) {
      case 'ctrl': return 'Ctrl'
      case 'alt': return 'Alt'
      case 'shift': return 'Shift'
      case 'meta': return navigator.platform.includes('Mac') ? '⌘' : 'Win'
      default: return m
    }
  })
  return [...modifiers, shortcut.key].join(' + ')
}
</script>

<template>
  <div class="min-h-screen bg-gray-50 dark:bg-gray-900 py-8">
    <div class="max-w-4xl mx-auto px-4">
      <!-- Header -->
      <div class="text-center mb-8">
        <h1 class="text-3xl font-bold text-gray-900 dark:text-gray-100 mb-2">Help Center</h1>
        <p class="text-gray-600 dark:text-gray-400">Learn how to get the most out of AEGIS</p>
      </div>

      <!-- Tabs -->
      <TabGroup>
        <TabList class="flex space-x-1 bg-white dark:bg-gray-800 rounded-xl p-1 shadow-sm border border-gray-200 dark:border-gray-700 mb-6">
          <Tab
            v-for="tab in tabs"
            :key="tab.name"
            v-slot="{ selected }"
            as="template"
          >
            <button
              :class="[
                'flex items-center gap-2 w-full rounded-lg py-2.5 px-4 text-sm font-medium leading-5 transition-colors',
                selected
                  ? 'bg-aegis-100 dark:bg-aegis-900 text-aegis-700 dark:text-aegis-300'
                  : 'text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-700'
              ]"
            >
              <component :is="tab.icon" class="h-5 w-5" />
              {{ tab.name }}
            </button>
          </Tab>
        </TabList>

        <TabPanels>
          <!-- Getting Started -->
          <TabPanel>
            <div class="bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-gray-200 dark:border-gray-700 p-6">
              <h2 class="text-xl font-semibold text-gray-900 dark:text-gray-100 mb-6">Getting Started with AEGIS</h2>

              <div class="space-y-6">
                <div
                  v-for="(step, index) in gettingStartedSteps"
                  :key="step.title"
                  class="flex gap-4"
                >
                  <div class="flex-shrink-0 w-10 h-10 rounded-full bg-aegis-100 dark:bg-aegis-900 flex items-center justify-center">
                    <span class="text-aegis-600 dark:text-aegis-400 font-semibold">{{ index + 1 }}</span>
                  </div>
                  <div class="flex-1">
                    <div class="flex items-center gap-2">
                      <component :is="step.icon" class="h-5 w-5 text-gray-400" />
                      <h3 class="font-medium text-gray-900 dark:text-gray-100">{{ step.title }}</h3>
                    </div>
                    <p class="mt-1 text-sm text-gray-600 dark:text-gray-400">{{ step.description }}</p>
                    <RouterLink
                      :to="step.link"
                      class="inline-flex items-center gap-1 mt-2 text-sm text-aegis-600 dark:text-aegis-400 hover:underline"
                    >
                      Get started
                      <ArrowTopRightOnSquareIcon class="h-4 w-4" />
                    </RouterLink>
                  </div>
                </div>
              </div>

              <!-- Quick Tips -->
              <div class="mt-8 p-4 bg-aegis-50 dark:bg-aegis-900/30 rounded-lg border border-aegis-100 dark:border-aegis-800">
                <h3 class="font-medium text-aegis-900 dark:text-aegis-100 mb-2">Quick Tips</h3>
                <ul class="space-y-2 text-sm text-aegis-700 dark:text-aegis-300">
                  <li>Press <kbd class="px-1.5 py-0.5 bg-white dark:bg-gray-800 rounded border text-xs">/</kbd> anywhere to quickly search</li>
                  <li>Use <kbd class="px-1.5 py-0.5 bg-white dark:bg-gray-800 rounded border text-xs">Ctrl+K</kbd> to open the command palette</li>
                  <li>Press <kbd class="px-1.5 py-0.5 bg-white dark:bg-gray-800 rounded border text-xs">?</kbd> to see all keyboard shortcuts</li>
                </ul>
              </div>
            </div>
          </TabPanel>

          <!-- Keyboard Shortcuts -->
          <TabPanel>
            <div class="bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-gray-200 dark:border-gray-700 p-6">
              <h2 class="text-xl font-semibold text-gray-900 dark:text-gray-100 mb-6">Keyboard Shortcuts</h2>

              <div class="space-y-8">
                <div v-for="category in shortcutsByCategory" :key="category.name">
                  <h3 class="text-sm font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wide mb-3">
                    {{ category.name }}
                  </h3>
                  <div class="space-y-2">
                    <div
                      v-for="shortcut in category.shortcuts"
                      :key="shortcut.action"
                      class="flex items-center justify-between py-2 px-3 bg-gray-50 dark:bg-gray-700/50 rounded-lg"
                    >
                      <span class="text-gray-700 dark:text-gray-300">{{ shortcut.description }}</span>
                      <kbd class="px-2 py-1 bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-600 rounded text-sm font-mono text-gray-600 dark:text-gray-400">
                        {{ formatShortcut(shortcut) }}
                      </kbd>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </TabPanel>

          <!-- FAQ -->
          <TabPanel>
            <div class="bg-white dark:bg-gray-800 rounded-xl shadow-sm border border-gray-200 dark:border-gray-700 p-6">
              <div class="flex items-center justify-between mb-6">
                <h2 class="text-xl font-semibold text-gray-900 dark:text-gray-100">Frequently Asked Questions</h2>
                <div class="relative">
                  <MagnifyingGlassIcon class="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
                  <input
                    v-model="searchQuery"
                    type="text"
                    placeholder="Search FAQs..."
                    class="pl-9 pr-4 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100"
                  />
                </div>
              </div>

              <div class="space-y-3">
                <Disclosure v-for="faq in filteredFaqs" :key="faq.id" v-slot="{ open }">
                  <DisclosureButton
                    class="flex items-center justify-between w-full px-4 py-3 text-left bg-gray-50 dark:bg-gray-700/50 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
                  >
                    <span class="font-medium text-gray-900 dark:text-gray-100">{{ faq.question }}</span>
                    <ChevronDownIcon
                      :class="[
                        'h-5 w-5 text-gray-500 transition-transform',
                        open && 'transform rotate-180'
                      ]"
                    />
                  </DisclosureButton>
                  <DisclosurePanel class="px-4 py-3 text-gray-600 dark:text-gray-400">
                    {{ faq.answer }}
                  </DisclosurePanel>
                </Disclosure>
              </div>

              <div v-if="filteredFaqs.length === 0" class="text-center py-8 text-gray-500 dark:text-gray-400">
                No FAQs match your search.
              </div>
            </div>
          </TabPanel>
        </TabPanels>
      </TabGroup>

      <!-- Contact Support -->
      <div class="mt-8 text-center">
        <p class="text-gray-600 dark:text-gray-400 mb-2">Can't find what you're looking for?</p>
        <a
          href="mailto:support@aegis.ai"
          class="inline-flex items-center gap-2 text-aegis-600 dark:text-aegis-400 hover:underline"
        >
          Contact Support
          <ArrowTopRightOnSquareIcon class="h-4 w-4" />
        </a>
      </div>
    </div>
  </div>
</template>
