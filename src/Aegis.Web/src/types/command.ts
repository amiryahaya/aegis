export type CommandCategory =
  | 'navigation'
  | 'action'
  | 'search'
  | 'settings'
  | 'session'
  | 'workspace'
  | 'help'

export interface Command {
  id: string
  label: string
  description?: string
  category: CommandCategory
  icon?: string
  shortcut?: string
  keywords?: string[]
  action: () => void | Promise<unknown>
  disabled?: boolean | (() => boolean)
  hidden?: boolean | (() => boolean)
}

export interface CommandGroup {
  category: CommandCategory
  label: string
  commands: Command[]
}

export interface CommandPaletteState {
  isOpen: boolean
  query: string
  selectedIndex: number
  recentCommands: string[]
}

export const CATEGORY_LABELS: Record<CommandCategory, string> = {
  navigation: 'Navigation',
  action: 'Actions',
  search: 'Search',
  settings: 'Settings',
  session: 'Sessions',
  workspace: 'Workspaces',
  help: 'Help'
}

export const CATEGORY_ORDER: CommandCategory[] = [
  'action',
  'navigation',
  'session',
  'workspace',
  'search',
  'settings',
  'help'
]
