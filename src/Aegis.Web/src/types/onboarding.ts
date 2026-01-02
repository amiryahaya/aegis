export type TourStepPlacement = 'top' | 'bottom' | 'left' | 'right' | 'center'

export interface TourStep {
  id: string
  title: string
  content: string
  target?: string // CSS selector for the target element
  placement?: TourStepPlacement
  spotlightPadding?: number
  showSkip?: boolean
  showProgress?: boolean
  action?: () => void | Promise<void>
  beforeShow?: () => boolean | Promise<boolean>
}

export interface Tour {
  id: string
  name: string
  description?: string
  steps: TourStep[]
  onComplete?: () => void
  onSkip?: () => void
}

export interface OnboardingState {
  hasCompletedWelcome: boolean
  completedTours: string[]
  skippedTours: string[]
  currentTour: string | null
  currentStep: number
  preferences: OnboardingPreferences
}

export interface OnboardingPreferences {
  showTipsOnStartup: boolean
  showFeatureAnnouncements: boolean
  reducedMotion: boolean
}

export interface WelcomeSlide {
  id: string
  title: string
  description: string
  icon: string
  image?: string
}

export const DEFAULT_ONBOARDING_STATE: OnboardingState = {
  hasCompletedWelcome: false,
  completedTours: [],
  skippedTours: [],
  currentTour: null,
  currentStep: 0,
  preferences: {
    showTipsOnStartup: true,
    showFeatureAnnouncements: true,
    reducedMotion: false
  }
}

export const WELCOME_SLIDES: WelcomeSlide[] = [
  {
    id: 'welcome',
    title: 'Welcome to Aegis',
    description: 'Your intelligent RAG-powered knowledge assistant. Ask questions, explore documents, and get accurate answers with source citations.',
    icon: 'SparklesIcon'
  },
  {
    id: 'workspaces',
    title: 'Organize with Workspaces',
    description: 'Create workspaces to organize your documents and data sources. Each workspace can have its own settings and access controls.',
    icon: 'FolderIcon'
  },
  {
    id: 'chat',
    title: 'Conversational AI',
    description: 'Ask questions naturally and get accurate answers with source citations. The AI understands context and can follow up on previous questions.',
    icon: 'ChatBubbleLeftRightIcon'
  },
  {
    id: 'collaborate',
    title: 'Collaborate with Your Team',
    description: 'Share workspaces, sessions, and insights with your team. Add comments, track activity, and work together in real-time.',
    icon: 'UserGroupIcon'
  }
]

export const MAIN_TOUR_STEPS: TourStep[] = [
  {
    id: 'sidebar',
    title: 'Navigation Sidebar',
    content: 'Access all main features from here: Dashboard, Chat, Sessions, Workspaces, and more.',
    target: '[data-tour="sidebar"]',
    placement: 'right',
    showProgress: true
  },
  {
    id: 'search',
    title: 'Global Search',
    content: 'Search across all your sessions, documents, and workspaces. Press "/" to focus the search bar quickly.',
    target: '[data-tour="search"]',
    placement: 'bottom',
    showProgress: true
  },
  {
    id: 'command-palette',
    title: 'Command Palette',
    content: 'Press Cmd+K (or Ctrl+K) to open the command palette for quick navigation and actions.',
    target: '[data-tour="command-palette"]',
    placement: 'bottom',
    showProgress: true
  },
  {
    id: 'new-chat',
    title: 'Start a Conversation',
    content: 'Click here to start a new chat session. Ask questions about your documents and get AI-powered answers.',
    target: '[data-tour="new-chat"]',
    placement: 'bottom',
    showProgress: true
  },
  {
    id: 'notifications',
    title: 'Stay Updated',
    content: 'View notifications about document processing, team activity, and system updates.',
    target: '[data-tour="notifications"]',
    placement: 'bottom',
    showProgress: true
  },
  {
    id: 'theme',
    title: 'Light & Dark Mode',
    content: 'Toggle between light and dark themes to suit your preference. You can also press "t" as a shortcut.',
    target: '[data-tour="theme"]',
    placement: 'bottom',
    showProgress: true
  }
]
