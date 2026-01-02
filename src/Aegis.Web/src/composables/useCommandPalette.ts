import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import type { Command, CommandGroup, CommandCategory, CommandPaletteState } from '@/types/command'
import { CATEGORY_LABELS, CATEGORY_ORDER } from '@/types/command'

const MAX_RECENT_COMMANDS = 5
const RECENT_COMMANDS_KEY = 'aegis_recent_commands'

// Global state for command palette
const state = ref<CommandPaletteState>({
  isOpen: false,
  query: '',
  selectedIndex: 0,
  recentCommands: []
})

// Command registry
const commands = ref<Command[]>([])

export function useCommandPalette() {
  const router = useRouter()
  const authStore = useAuthStore()

  // Load recent commands from localStorage
  function loadRecentCommands(): void {
    try {
      const stored = localStorage.getItem(RECENT_COMMANDS_KEY)
      if (stored) {
        state.value.recentCommands = JSON.parse(stored)
      }
    } catch {
      state.value.recentCommands = []
    }
  }

  // Save recent commands to localStorage
  function saveRecentCommands(): void {
    try {
      localStorage.setItem(RECENT_COMMANDS_KEY, JSON.stringify(state.value.recentCommands))
    } catch {
      // Ignore storage errors
    }
  }

  // Add command to recent
  function addToRecent(commandId: string): void {
    const recentIndex = state.value.recentCommands.indexOf(commandId)
    if (recentIndex !== -1) {
      state.value.recentCommands.splice(recentIndex, 1)
    }
    state.value.recentCommands.unshift(commandId)
    state.value.recentCommands = state.value.recentCommands.slice(0, MAX_RECENT_COMMANDS)
    saveRecentCommands()
  }

  // Fuzzy search implementation
  function fuzzyMatch(text: string, query: string): { matches: boolean; score: number } {
    if (!query) return { matches: true, score: 0 }

    const textLower = text.toLowerCase()
    const queryLower = query.toLowerCase()

    // Exact match gets highest score
    if (textLower === queryLower) return { matches: true, score: 100 }

    // Starts with query
    if (textLower.startsWith(queryLower)) return { matches: true, score: 90 }

    // Contains query
    if (textLower.includes(queryLower)) return { matches: true, score: 70 }

    // Fuzzy match - each character in query must appear in order
    let queryIndex = 0
    let score = 0
    let consecutiveMatches = 0

    for (let i = 0; i < textLower.length && queryIndex < queryLower.length; i++) {
      if (textLower[i] === queryLower[queryIndex]) {
        queryIndex++
        consecutiveMatches++
        score += consecutiveMatches * 2 // Bonus for consecutive matches
      } else {
        consecutiveMatches = 0
      }
    }

    if (queryIndex === queryLower.length) {
      return { matches: true, score: score + 10 }
    }

    return { matches: false, score: 0 }
  }

  // Filter commands by query
  function filterCommands(query: string): Command[] {
    return commands.value
      .filter(cmd => {
        // Check if command is hidden
        if (typeof cmd.hidden === 'function' ? cmd.hidden() : cmd.hidden) {
          return false
        }

        if (!query) return true

        // Match against label, description, and keywords
        const labelMatch = fuzzyMatch(cmd.label, query)
        const descMatch = cmd.description ? fuzzyMatch(cmd.description, query) : { matches: false, score: 0 }
        const keywordMatch = cmd.keywords?.some(k => fuzzyMatch(k, query).matches) || false

        return labelMatch.matches || descMatch.matches || keywordMatch
      })
      .map(cmd => {
        const labelMatch = fuzzyMatch(cmd.label, query)
        const descMatch = cmd.description ? fuzzyMatch(cmd.description, query) : { matches: false, score: 0 }
        return {
          command: cmd,
          score: Math.max(labelMatch.score, descMatch.score)
        }
      })
      .sort((a, b) => {
        // Prioritize recent commands
        const aRecent = state.value.recentCommands.indexOf(a.command.id)
        const bRecent = state.value.recentCommands.indexOf(b.command.id)

        if (aRecent !== -1 && bRecent === -1) return -1
        if (bRecent !== -1 && aRecent === -1) return 1
        if (aRecent !== -1 && bRecent !== -1) return aRecent - bRecent

        return b.score - a.score
      })
      .map(item => item.command)
  }

  // Group commands by category
  const groupedCommands = computed((): CommandGroup[] => {
    const filtered = filterCommands(state.value.query)
    const groups: Map<CommandCategory, Command[]> = new Map()

    for (const cmd of filtered) {
      const existing = groups.get(cmd.category) || []
      existing.push(cmd)
      groups.set(cmd.category, existing)
    }

    return CATEGORY_ORDER
      .filter(cat => groups.has(cat))
      .map(cat => ({
        category: cat,
        label: CATEGORY_LABELS[cat],
        commands: groups.get(cat)!
      }))
  })

  // Flat list of filtered commands
  const filteredCommands = computed((): Command[] => {
    return groupedCommands.value.flatMap(g => g.commands)
  })

  // Open command palette
  function open(): void {
    state.value.isOpen = true
    state.value.query = ''
    state.value.selectedIndex = 0
  }

  // Close command palette
  function close(): void {
    state.value.isOpen = false
    state.value.query = ''
    state.value.selectedIndex = 0
  }

  // Toggle command palette
  function toggle(): void {
    if (state.value.isOpen) {
      close()
    } else {
      open()
    }
  }

  // Execute command
  async function executeCommand(command: Command): Promise<void> {
    // Check if disabled
    if (typeof command.disabled === 'function' ? command.disabled() : command.disabled) {
      return
    }

    close()
    addToRecent(command.id)
    await command.action()
  }

  // Execute selected command
  async function executeSelected(): Promise<void> {
    const cmds = filteredCommands.value
    if (cmds.length > 0 && state.value.selectedIndex < cmds.length) {
      await executeCommand(cmds[state.value.selectedIndex])
    }
  }

  // Navigate selection
  function selectNext(): void {
    const cmds = filteredCommands.value
    if (cmds.length > 0) {
      state.value.selectedIndex = (state.value.selectedIndex + 1) % cmds.length
    }
  }

  function selectPrevious(): void {
    const cmds = filteredCommands.value
    if (cmds.length > 0) {
      state.value.selectedIndex = (state.value.selectedIndex - 1 + cmds.length) % cmds.length
    }
  }

  function selectIndex(index: number): void {
    const cmds = filteredCommands.value
    if (index >= 0 && index < cmds.length) {
      state.value.selectedIndex = index
    }
  }

  // Update query
  function setQuery(query: string): void {
    state.value.query = query
    state.value.selectedIndex = 0
  }

  // Register default commands
  function registerDefaultCommands(): void {
    const defaultCommands: Command[] = [
      // Navigation
      {
        id: 'nav-dashboard',
        label: 'Go to Dashboard',
        description: 'View your dashboard overview',
        category: 'navigation',
        icon: 'HomeIcon',
        shortcut: 'g h',
        keywords: ['home', 'main', 'overview'],
        action: () => router.push('/')
      },
      {
        id: 'nav-chat',
        label: 'Go to Chat',
        description: 'Start a new conversation',
        category: 'navigation',
        icon: 'ChatBubbleLeftRightIcon',
        shortcut: 'g c',
        keywords: ['query', 'ask', 'conversation'],
        action: () => router.push('/chat')
      },
      {
        id: 'nav-sessions',
        label: 'Go to Sessions',
        description: 'View all chat sessions',
        category: 'navigation',
        icon: 'ClockIcon',
        shortcut: 'g s',
        keywords: ['history', 'conversations', 'past'],
        action: () => router.push('/sessions')
      },
      {
        id: 'nav-workspaces',
        label: 'Go to Workspaces',
        description: 'Manage your workspaces',
        category: 'navigation',
        icon: 'FolderIcon',
        shortcut: 'g w',
        keywords: ['projects', 'folders', 'documents'],
        action: () => router.push('/workspaces')
      },
      {
        id: 'nav-search',
        label: 'Go to Search',
        description: 'Search across all content',
        category: 'navigation',
        icon: 'MagnifyingGlassIcon',
        shortcut: '/',
        keywords: ['find', 'lookup'],
        action: () => router.push('/search')
      },
      {
        id: 'nav-notifications',
        label: 'Go to Notifications',
        description: 'View your notifications',
        category: 'navigation',
        icon: 'BellIcon',
        shortcut: 'g n',
        keywords: ['alerts', 'updates', 'messages'],
        action: () => router.push('/notifications')
      },
      {
        id: 'nav-activity',
        label: 'Go to Activity Feed',
        description: 'View recent activity',
        category: 'navigation',
        icon: 'RssIcon',
        keywords: ['feed', 'updates', 'timeline'],
        action: () => router.push('/activity')
      },
      {
        id: 'nav-profile',
        label: 'Go to Profile',
        description: 'View and edit your profile',
        category: 'navigation',
        icon: 'UserIcon',
        shortcut: 'g p',
        keywords: ['account', 'me', 'user'],
        action: () => router.push('/profile')
      },
      {
        id: 'nav-settings',
        label: 'Go to Settings',
        description: 'Configure your preferences',
        category: 'navigation',
        icon: 'Cog6ToothIcon',
        keywords: ['preferences', 'options', 'config'],
        action: () => router.push('/settings')
      },
      {
        id: 'nav-admin',
        label: 'Go to Admin Dashboard',
        description: 'System administration',
        category: 'navigation',
        icon: 'ShieldCheckIcon',
        keywords: ['manage', 'system', 'admin'],
        action: () => router.push('/admin'),
        hidden: () => !authStore.isAdmin
      },
      {
        id: 'nav-help',
        label: 'Go to Help Center',
        description: 'Get help and documentation',
        category: 'navigation',
        icon: 'QuestionMarkCircleIcon',
        shortcut: 'g ?',
        keywords: ['docs', 'faq', 'support'],
        action: () => router.push('/help')
      },

      // Actions
      {
        id: 'action-new-session',
        label: 'New Chat Session',
        description: 'Start a new chat session',
        category: 'action',
        icon: 'PlusIcon',
        keywords: ['create', 'start', 'begin'],
        action: async () => {
          router.push('/chat')
        }
      },
      {
        id: 'action-new-workspace',
        label: 'Create Workspace',
        description: 'Create a new workspace',
        category: 'action',
        icon: 'FolderPlusIcon',
        keywords: ['new', 'add', 'project'],
        action: () => {
          router.push('/workspaces?action=create')
        }
      },
      {
        id: 'action-toggle-theme',
        label: 'Toggle Dark Mode',
        description: 'Switch between light and dark theme',
        category: 'settings',
        icon: 'MoonIcon',
        shortcut: 't',
        keywords: ['theme', 'light', 'night'],
        action: () => {
          document.documentElement.classList.toggle('dark')
          const isDark = document.documentElement.classList.contains('dark')
          localStorage.setItem('theme', isDark ? 'dark' : 'light')
        }
      },
      {
        id: 'action-logout',
        label: 'Sign Out',
        description: 'Log out of your account',
        category: 'action',
        icon: 'ArrowRightOnRectangleIcon',
        keywords: ['logout', 'exit', 'leave'],
        action: async () => {
          await authStore.logout()
          router.push('/login')
        }
      },

      // Session actions
      {
        id: 'session-export',
        label: 'Export Sessions',
        description: 'Export your sessions as JSON or Markdown',
        category: 'session',
        icon: 'ArrowDownTrayIcon',
        keywords: ['download', 'backup', 'save'],
        action: () => {
          router.push('/sessions?action=export')
        }
      },

      // Help
      {
        id: 'help-shortcuts',
        label: 'Keyboard Shortcuts',
        description: 'View all keyboard shortcuts',
        category: 'help',
        icon: 'CommandLineIcon',
        shortcut: '?',
        keywords: ['keys', 'hotkeys', 'bindings'],
        action: () => router.push('/help#shortcuts')
      },
      {
        id: 'help-faq',
        label: 'FAQ',
        description: 'Frequently asked questions',
        category: 'help',
        icon: 'QuestionMarkCircleIcon',
        keywords: ['questions', 'answers', 'common'],
        action: () => router.push('/help#faq')
      }
    ]

    commands.value = defaultCommands
  }

  // Register a custom command
  function registerCommand(command: Command): void {
    const existingIndex = commands.value.findIndex(c => c.id === command.id)
    if (existingIndex !== -1) {
      commands.value[existingIndex] = command
    } else {
      commands.value.push(command)
    }
  }

  // Unregister a command
  function unregisterCommand(commandId: string): void {
    commands.value = commands.value.filter(c => c.id !== commandId)
  }

  // Keyboard event handler
  function handleKeyDown(event: KeyboardEvent): void {
    // Open with Cmd+K or Ctrl+K
    if ((event.metaKey || event.ctrlKey) && event.key === 'k') {
      event.preventDefault()
      toggle()
      return
    }

    // Only handle other keys when palette is open
    if (!state.value.isOpen) return

    switch (event.key) {
      case 'Escape':
        event.preventDefault()
        close()
        break
      case 'ArrowDown':
        event.preventDefault()
        selectNext()
        break
      case 'ArrowUp':
        event.preventDefault()
        selectPrevious()
        break
      case 'Enter':
        event.preventDefault()
        executeSelected()
        break
      case 'Tab':
        event.preventDefault()
        if (event.shiftKey) {
          selectPrevious()
        } else {
          selectNext()
        }
        break
    }
  }

  // Setup and teardown
  onMounted(() => {
    loadRecentCommands()
    registerDefaultCommands()
    window.addEventListener('keydown', handleKeyDown)
  })

  onUnmounted(() => {
    window.removeEventListener('keydown', handleKeyDown)
  })

  return {
    // State
    state,
    isOpen: computed(() => state.value.isOpen),
    query: computed(() => state.value.query),
    selectedIndex: computed(() => state.value.selectedIndex),

    // Commands
    commands,
    groupedCommands,
    filteredCommands,

    // Actions
    open,
    close,
    toggle,
    setQuery,
    selectNext,
    selectPrevious,
    selectIndex,
    executeCommand,
    executeSelected,

    // Registry
    registerCommand,
    unregisterCommand
  }
}
