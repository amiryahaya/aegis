import { test, expect } from '@playwright/test'
import { testUser, mockResponses } from './fixtures'

// Helper to set up authenticated state
async function setupAuth(page: import('@playwright/test').Page) {
  await page.goto('/login')
  await page.evaluate((data) => {
    localStorage.setItem('token', data.token)
    localStorage.setItem('user', JSON.stringify(data.user))
  }, mockResponses.loginSuccess)
}

test.describe('Chat', () => {
  test.beforeEach(async ({ page }) => {
    await setupAuth(page)

    // Mock sessions endpoint
    await page.route('**/api/sessions', async route => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ items: [], totalCount: 0 }),
        })
      } else {
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'new-session-1',
            title: 'New Chat',
            type: 'QuickQuery',
            status: 'Active',
            turnCount: 0,
            createdAt: new Date().toISOString(),
          }),
        })
      }
    })
  })

  test.describe('Chat Interface', () => {
    test('should display chat view with input area', async ({ page }) => {
      await page.goto('/chat')

      // Check for the ChatInput component (textarea)
      await expect(page.locator('textarea')).toBeVisible()

      // Check for empty state message
      await expect(page.locator('text=Start a conversation')).toBeVisible()
    })

    test('should show empty state for new chat', async ({ page }) => {
      await page.goto('/chat')

      // Check empty state
      await expect(page.locator('text=Start a conversation')).toBeVisible()
      await expect(page.locator('text=Ask questions about your documents')).toBeVisible()
    })

    test('should have send button', async ({ page }) => {
      await page.goto('/chat')

      // The ChatInput has a button with PaperAirplaneIcon
      const sendButton = page.locator('button:has(svg)').last()
      await expect(sendButton).toBeVisible()
    })

    test('should display placeholder text in input', async ({ page }) => {
      await page.goto('/chat')

      const textarea = page.locator('textarea')
      await expect(textarea).toHaveAttribute('placeholder', /Ask anything about your documents/)
    })
  })

  test.describe('Chat with existing session', () => {
    test('should display session header when session exists', async ({ page }) => {
      const mockSession = {
        id: 'session-1',
        title: 'Test Session',
        type: 'QuickQuery',
        status: 'Active',
        turnCount: 2,
        createdAt: new Date().toISOString(),
      }

      await page.route('**/api/sessions/session-1', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(mockSession),
        })
      })

      await page.route('**/api/sessions/session-1/conversation', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            {
              id: 'turn-1',
              sessionId: 'session-1',
              userQuery: 'What is RAG?',
              systemResponse: 'RAG stands for Retrieval-Augmented Generation...',
              sources: [],
              createdAt: new Date().toISOString(),
            },
          ]),
        })
      })

      await page.goto('/chat/session-1')

      // Should display session title
      await expect(page.locator('text=Test Session')).toBeVisible({ timeout: 5000 })
    })

    test('should display conversation turns', async ({ page }) => {
      await page.route('**/api/sessions/session-1', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'session-1',
            title: 'Test Session',
            type: 'QuickQuery',
            status: 'Active',
            turnCount: 1,
            createdAt: new Date().toISOString(),
          }),
        })
      })

      await page.route('**/api/sessions/session-1/conversation', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            {
              id: 'turn-1',
              sessionId: 'session-1',
              userQuery: 'What is machine learning?',
              systemResponse: 'Machine learning is a subset of AI...',
              sources: [
                {
                  documentId: 'doc-1',
                  documentName: 'ML Guide.pdf',
                  relevanceScore: 0.95,
                  excerpt: 'Machine learning enables...',
                },
              ],
              createdAt: new Date().toISOString(),
            },
          ]),
        })
      })

      await page.goto('/chat/session-1')

      // Should display the user query
      await expect(page.locator('text=What is machine learning?')).toBeVisible({ timeout: 5000 })

      // Should display AEGIS label
      await expect(page.locator('text=AEGIS')).toBeVisible()
    })

    test('should display sources when available', async ({ page }) => {
      await page.route('**/api/sessions/session-1', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'session-1',
            title: 'Test Session',
            type: 'QuickQuery',
            status: 'Active',
            turnCount: 1,
            createdAt: new Date().toISOString(),
          }),
        })
      })

      await page.route('**/api/sessions/session-1/conversation', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            {
              id: 'turn-1',
              sessionId: 'session-1',
              userQuery: 'Test query',
              systemResponse: 'Test response',
              sources: [
                {
                  documentId: 'doc-1',
                  documentName: 'Test Doc.pdf',
                  relevanceScore: 0.92,
                  excerpt: 'Test excerpt',
                },
              ],
              createdAt: new Date().toISOString(),
            },
          ]),
        })
      })

      await page.goto('/chat/session-1')

      // Should show sources section
      await expect(page.locator('text=1 source')).toBeVisible({ timeout: 5000 })
    })
  })

  test.describe('Chat Actions', () => {
    test('should have session menu when session exists', async ({ page }) => {
      await page.route('**/api/sessions/session-1', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'session-1',
            title: 'Test Session',
            type: 'QuickQuery',
            status: 'Active',
            turnCount: 0,
            createdAt: new Date().toISOString(),
          }),
        })
      })

      await page.route('**/api/sessions/session-1/conversation', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([]),
        })
      })

      await page.goto('/chat/session-1')

      // Should have the menu button (EllipsisVerticalIcon)
      await expect(page.locator('button:has(svg)')).toBeVisible()
    })
  })
})

test.describe('Chat Accessibility', () => {
  test('should have accessible form elements', async ({ page }) => {
    await setupAuth(page)

    await page.route('**/api/sessions', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [], totalCount: 0 }),
      })
    })

    await page.goto('/chat')

    // Textarea should be focusable
    const textarea = page.locator('textarea')
    await textarea.focus()
    await expect(textarea).toBeFocused()
  })
})
