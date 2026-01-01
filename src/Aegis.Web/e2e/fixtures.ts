import { test as base, expect } from '@playwright/test'

/**
 * Test fixtures for AEGIS E2E tests
 */

// Test user credentials
export const testUser = {
  email: 'test@aegis.local',
  password: 'TestPassword123!',
  name: 'Test User',
}

// Mock API responses
export const mockResponses = {
  loginSuccess: {
    token: 'mock-jwt-token',
    refreshToken: 'mock-refresh-token',
    user: {
      id: 'user-1',
      email: testUser.email,
      name: testUser.name,
      role: 'Admin',
      teamId: 'team-1',
    },
  },
  sessions: [
    {
      id: 'session-1',
      title: 'Test Session 1',
      type: 'QuickQuery',
      status: 'Active',
      turnCount: 5,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    },
    {
      id: 'session-2',
      title: 'Test Session 2',
      type: 'Research',
      status: 'Active',
      turnCount: 10,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    },
  ],
  workspaces: [
    {
      id: 'workspace-1',
      name: 'Test Workspace',
      description: 'A test workspace',
      documentCount: 5,
      createdAt: new Date().toISOString(),
    },
  ],
}

/**
 * Extended test with authentication helpers
 */
export const test = base.extend<{
  authenticatedPage: typeof base
}>({
  authenticatedPage: async ({ page }, use) => {
    // Set up authentication state
    await page.goto('/login')

    // Mock the API response for login
    await page.route('**/api/auth/login', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(mockResponses.loginSuccess),
      })
    })

    // Fill in login form
    await page.fill('input[type="email"]', testUser.email)
    await page.fill('input[type="password"]', testUser.password)
    await page.click('button[type="submit"]')

    // Wait for navigation to dashboard
    await page.waitForURL('/')

    await use(base)
  },
})

/**
 * Page object models
 */
export class LoginPage {
  constructor(private page: typeof base.prototype.page) {}

  async goto() {
    await this.page.goto('/login')
  }

  async login(email: string, password: string) {
    await this.page.fill('input[type="email"]', email)
    await this.page.fill('input[type="password"]', password)
    await this.page.click('button[type="submit"]')
  }

  async getErrorMessage() {
    return this.page.locator('[data-testid="error-message"]').textContent()
  }
}

export class DashboardPage {
  constructor(private page: typeof base.prototype.page) {}

  async goto() {
    await this.page.goto('/')
  }

  async getSessionCount() {
    return this.page.locator('[data-testid="session-count"]').textContent()
  }

  async clickNewChat() {
    await this.page.click('button:has-text("New Chat")')
  }
}

export class ChatPage {
  constructor(private page: typeof base.prototype.page) {}

  async goto(sessionId?: string) {
    if (sessionId) {
      await this.page.goto(`/chat/${sessionId}`)
    } else {
      await this.page.goto('/chat')
    }
  }

  async sendMessage(message: string) {
    await this.page.fill('textarea[placeholder*="Type your message"]', message)
    await this.page.click('button[aria-label="Send message"]')
  }

  async getMessages() {
    return this.page.locator('[data-testid="chat-message"]').all()
  }

  async waitForResponse() {
    await this.page.waitForSelector('[data-testid="assistant-message"]')
  }
}

export class SessionsPage {
  constructor(private page: typeof base.prototype.page) {}

  async goto() {
    await this.page.goto('/sessions')
  }

  async getSessionCards() {
    return this.page.locator('[data-testid="session-card"]').all()
  }

  async searchSessions(query: string) {
    await this.page.fill('input[placeholder*="Search"]', query)
  }

  async deleteSession(sessionId: string) {
    await this.page.click(`[data-testid="session-${sessionId}"] button[aria-label="Delete"]`)
    await this.page.click('button:has-text("Delete")')
  }
}

export class WorkspacesPage {
  constructor(private page: typeof base.prototype.page) {}

  async goto() {
    await this.page.goto('/workspaces')
  }

  async getWorkspaceCards() {
    return this.page.locator('[data-testid="workspace-card"]').all()
  }

  async createWorkspace(name: string, description?: string) {
    await this.page.click('button:has-text("Create Workspace")')
    await this.page.fill('input[name="name"]', name)
    if (description) {
      await this.page.fill('textarea[name="description"]', description)
    }
    await this.page.click('button:has-text("Create")')
  }
}

export class SearchPage {
  constructor(private page: typeof base.prototype.page) {}

  async goto() {
    await this.page.goto('/search')
  }

  async search(query: string) {
    await this.page.fill('input[type="text"]', query)
    await this.page.press('input[type="text"]', 'Enter')
  }

  async getResults() {
    return this.page.locator('[data-testid="search-result"]').all()
  }

  async filterByType(type: string) {
    await this.page.click(`button:has-text("${type}")`)
  }
}

export { expect }
