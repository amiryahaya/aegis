import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  // Scroll behavior for route transitions
  scrollBehavior(to, _from, savedPosition) {
    if (savedPosition) {
      return savedPosition
    }
    if (to.hash) {
      return { el: to.hash, behavior: 'smooth' }
    }
    return { top: 0 }
  },
  routes: [
    // Auth routes - small chunk
    {
      path: '/login',
      name: 'login',
      component: () => import(/* webpackChunkName: "auth" */ '@/views/LoginView.vue'),
      meta: { requiresAuth: false, title: 'Login' }
    },
    // Dashboard - core experience
    {
      path: '/',
      name: 'dashboard',
      component: () => import(/* webpackChunkName: "dashboard" */ '@/views/DashboardView.vue'),
      meta: { requiresAuth: true, title: 'Dashboard' }
    },
    // Chat - primary feature, should load fast
    {
      path: '/chat',
      name: 'chat',
      component: () => import(/* webpackChunkName: "chat" */ '@/views/ChatView.vue'),
      meta: { requiresAuth: true, title: 'Chat' }
    },
    {
      path: '/chat/:sessionId',
      name: 'chat-session',
      component: () => import(/* webpackChunkName: "chat" */ '@/views/ChatView.vue'),
      meta: { requiresAuth: true, title: 'Chat Session' }
    },
    // Sessions management
    {
      path: '/sessions',
      name: 'sessions',
      component: () => import(/* webpackChunkName: "sessions" */ '@/views/SessionsView.vue'),
      meta: { requiresAuth: true, title: 'Sessions' }
    },
    // Workspaces - grouped together
    {
      path: '/workspaces',
      name: 'workspaces',
      component: () => import(/* webpackChunkName: "workspaces" */ '@/views/WorkspacesView.vue'),
      meta: { requiresAuth: true, title: 'Workspaces' }
    },
    {
      path: '/workspaces/:workspaceId',
      name: 'workspace-detail',
      component: () => import(/* webpackChunkName: "workspaces" */ '@/views/WorkspaceDetailView.vue'),
      meta: { requiresAuth: true, title: 'Workspace Details' }
    },
    // Notifications
    {
      path: '/notifications',
      name: 'notifications',
      component: () => import(/* webpackChunkName: "notifications" */ '@/views/NotificationsView.vue'),
      meta: { requiresAuth: true, title: 'Notifications' }
    },
    // Settings - less frequently accessed
    {
      path: '/settings',
      name: 'settings',
      component: () => import(/* webpackChunkName: "settings" */ '@/views/SettingsView.vue'),
      meta: { requiresAuth: true, title: 'Settings' }
    },
    // Admin - only for admins
    {
      path: '/admin',
      name: 'admin',
      component: () => import(/* webpackChunkName: "admin" */ '@/views/AdminView.vue'),
      meta: { requiresAuth: true, requiresAdmin: true, title: 'Admin Dashboard' }
    },
    // Search
    {
      path: '/search',
      name: 'search',
      component: () => import(/* webpackChunkName: "search" */ '@/views/SearchView.vue'),
      meta: { requiresAuth: true, title: 'Search' }
    },
    // Profile
    {
      path: '/profile',
      name: 'profile',
      component: () => import(/* webpackChunkName: "profile" */ '@/views/ProfileView.vue'),
      meta: { requiresAuth: true, title: 'Profile' }
    },
    // Help
    {
      path: '/help',
      name: 'help',
      component: () => import(/* webpackChunkName: "help" */ '@/views/HelpView.vue'),
      meta: { requiresAuth: true, title: 'Help Center' }
    },
    // Activity Feed
    {
      path: '/activity',
      name: 'activity',
      component: () => import(/* webpackChunkName: "activity" */ '@/views/ActivityFeedView.vue'),
      meta: { requiresAuth: true, title: 'Activity Feed' }
    },
    // Analytics
    {
      path: '/analytics',
      name: 'analytics',
      component: () => import(/* webpackChunkName: "analytics" */ '@/views/AnalyticsView.vue'),
      meta: { requiresAuth: true, title: 'Analytics' }
    },
    // Reports
    {
      path: '/reports',
      name: 'reports',
      component: () => import(/* webpackChunkName: "reports" */ '@/views/ReportsView.vue'),
      meta: { requiresAuth: true, title: 'Reports' }
    },
    // Integrations
    {
      path: '/integrations',
      name: 'integrations',
      component: () => import(/* webpackChunkName: "integrations" */ '@/views/IntegrationsView.vue'),
      meta: { requiresAuth: true, title: 'Integrations' }
    },
    // Audit Log (Admin)
    {
      path: '/audit',
      name: 'audit',
      component: () => import(/* webpackChunkName: "admin" */ '@/views/AuditLogView.vue'),
      meta: { requiresAuth: true, requiresAdmin: true, title: 'Audit Log' }
    },
    // Catch all - redirect to dashboard
    {
      path: '/:pathMatch(.*)*',
      redirect: '/'
    }
  ]
})

// Navigation guard for authentication and authorization
router.beforeEach((to, _from, next) => {
  const authStore = useAuthStore()

  // Update document title
  if (to.meta.title) {
    document.title = `${to.meta.title} | AEGIS`
  }

  if (to.meta.requiresAuth !== false && !authStore.isAuthenticated) {
    next({ name: 'login', query: { redirect: to.fullPath } })
  } else if (to.name === 'login' && authStore.isAuthenticated) {
    next({ name: 'dashboard' })
  } else if (to.meta.requiresAdmin && !authStore.isAdmin) {
    next({ name: 'dashboard' })
  } else {
    next()
  }
})

// Performance: Log slow navigations in development
if (import.meta.env.DEV) {
  let navigationStart: number

  router.beforeEach((_to, _from, next) => {
    navigationStart = performance.now()
    next()
  })

  router.afterEach((to) => {
    const duration = performance.now() - navigationStart
    if (duration > 100) {
      console.warn(`[Router] Slow navigation to ${to.path}: ${duration.toFixed(2)}ms`)
    }
  })
}

export default router
