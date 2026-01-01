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

test.describe('Sessions', () => {
  test.beforeEach(async ({ page }) => {
    await setupAuth(page)
  })

  test.describe('Sessions List', () => {
    test('should display sessions list', async ({ page }) => {
      await page.route('**/api/sessions*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: mockResponses.sessions,
            totalCount: mockResponses.sessions.length,
            pageNumber: 1,
            pageSize: 20,
          }),
        })
      })

      await page.goto('/sessions')

      // Should display sessions
      await expect(page.locator('text=Test Session 1')).toBeVisible({ timeout: 5000 })
      await expect(page.locator('text=Test Session 2')).toBeVisible()
    })

    test('should show empty state when no sessions', async ({ page }) => {
      await page.route('**/api/sessions*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [],
            totalCount: 0,
            pageNumber: 1,
            pageSize: 20,
          }),
        })
      })

      await page.goto('/sessions')

      // Should show empty state - check for "No sessions found"
      await expect(page.locator('text=No sessions found')).toBeVisible({ timeout: 5000 })
    })

    test('should have search input', async ({ page }) => {
      await page.route('**/api/sessions*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: mockResponses.sessions,
            totalCount: mockResponses.sessions.length,
          }),
        })
      })

      await page.goto('/sessions')

      // Search input should be visible
      const searchInput = page.locator('input[placeholder*="Search sessions"]')
      await expect(searchInput).toBeVisible()
    })

    test('should have status filter', async ({ page }) => {
      await page.route('**/api/sessions*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: mockResponses.sessions,
            totalCount: mockResponses.sessions.length,
          }),
        })
      })

      await page.goto('/sessions')

      // Status filter should be visible
      const statusFilter = page.locator('select:has(option:text("All Status"))')
      await expect(statusFilter).toBeVisible()
    })

    test('should have type filter', async ({ page }) => {
      await page.route('**/api/sessions*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: mockResponses.sessions,
            totalCount: mockResponses.sessions.length,
          }),
        })
      })

      await page.goto('/sessions')

      // Type filter should be visible
      const typeFilter = page.locator('select:has(option:text("All Types"))')
      await expect(typeFilter).toBeVisible()
    })
  })

  test.describe('Session CRUD', () => {
    test('should have new session button', async ({ page }) => {
      await page.route('**/api/sessions*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: mockResponses.sessions,
            totalCount: mockResponses.sessions.length,
          }),
        })
      })

      await page.goto('/sessions')

      // New Session button should be visible
      const newSessionButton = page.locator('button:has-text("New Session")')
      await expect(newSessionButton).toBeVisible()
    })

    test('should open create dialog when clicking new session', async ({ page }) => {
      await page.route('**/api/sessions*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: mockResponses.sessions,
            totalCount: mockResponses.sessions.length,
          }),
        })
      })

      await page.goto('/sessions')

      // Click new session button
      await page.click('button:has-text("New Session")')

      // Dialog should open
      await expect(page.locator('text=Create New Session')).toBeVisible({ timeout: 5000 })
    })

    test('should navigate to chat when clicking session', async ({ page }) => {
      await page.route('**/api/sessions*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: mockResponses.sessions,
            totalCount: mockResponses.sessions.length,
          }),
        })
      })

      await page.goto('/sessions')

      // Wait for sessions to load
      await expect(page.locator('text=Test Session 1')).toBeVisible({ timeout: 5000 })

      // Click on session card
      await page.click('text=Test Session 1')

      // Should navigate to chat view
      await expect(page).toHaveURL(/\/chat\/session-1/)
    })

    test('should show delete button on hover', async ({ page }) => {
      await page.route('**/api/sessions*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: mockResponses.sessions,
            totalCount: mockResponses.sessions.length,
          }),
        })
      })

      await page.goto('/sessions')

      // Wait for sessions to load
      await expect(page.locator('text=Test Session 1')).toBeVisible({ timeout: 5000 })

      // Hover over the session card to reveal actions
      await page.hover('.card:has-text("Test Session 1")')

      // Delete button should be visible (TrashIcon)
      await expect(page.locator('.card:has-text("Test Session 1") button[title="Delete"]')).toBeVisible()
    })
  })

  test.describe('Session Cards', () => {
    test('should display session type badge', async ({ page }) => {
      await page.route('**/api/sessions*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: mockResponses.sessions,
            totalCount: mockResponses.sessions.length,
          }),
        })
      })

      await page.goto('/sessions')

      // Should show session type
      await expect(page.locator('text=QuickQuery')).toBeVisible({ timeout: 5000 })
    })

    test('should display session status', async ({ page }) => {
      await page.route('**/api/sessions*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: mockResponses.sessions,
            totalCount: mockResponses.sessions.length,
          }),
        })
      })

      await page.goto('/sessions')

      // Should show session status
      await expect(page.locator('text=Active')).toBeVisible({ timeout: 5000 })
    })

    test('should display turn count', async ({ page }) => {
      await page.route('**/api/sessions*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: mockResponses.sessions,
            totalCount: mockResponses.sessions.length,
          }),
        })
      })

      await page.goto('/sessions')

      // Should show turn count
      await expect(page.locator('text=5 turns')).toBeVisible({ timeout: 5000 })
    })
  })

  test.describe('Session Pagination', () => {
    test('should show load more button when more sessions available', async ({ page }) => {
      const manySessions = Array.from({ length: 25 }, (_, i) => ({
        id: `session-${i + 1}`,
        title: `Session ${i + 1}`,
        type: 'QuickQuery',
        status: 'Active',
        turnCount: i + 1,
        createdAt: new Date().toISOString(),
      }))

      await page.route('**/api/sessions*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: manySessions.slice(0, 20),
            totalCount: 25,
            pageNumber: 1,
            pageSize: 20,
          }),
        })
      })

      await page.goto('/sessions')

      // Wait for sessions to load
      await expect(page.locator('text=Session 1')).toBeVisible({ timeout: 5000 })

      // Load more button should be visible
      await expect(page.locator('button:has-text("Load more")')).toBeVisible()
    })
  })
})
