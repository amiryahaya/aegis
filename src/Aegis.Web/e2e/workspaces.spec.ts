import { test, expect } from '@playwright/test'
import { mockResponses } from './fixtures'

// Helper to set up authenticated state
async function setupAuth(page: import('@playwright/test').Page) {
  await page.goto('/login')
  await page.evaluate((data) => {
    localStorage.setItem('token', data.token)
    localStorage.setItem('user', JSON.stringify(data.user))
  }, mockResponses.loginSuccess)
}

test.describe('Workspaces', () => {
  test.beforeEach(async ({ page }) => {
    await setupAuth(page)
  })

  test.describe('Workspaces List', () => {
    test('should display workspaces list', async ({ page }) => {
      await page.route('**/api/workspaces*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: mockResponses.workspaces,
            totalCount: mockResponses.workspaces.length,
          }),
        })
      })

      await page.goto('/workspaces')

      // Should display workspaces
      await expect(page.locator('text=Test Workspace')).toBeVisible({ timeout: 5000 })
    })

    test('should show empty state when no workspaces', async ({ page }) => {
      await page.route('**/api/workspaces*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [],
            totalCount: 0,
          }),
        })
      })

      await page.goto('/workspaces')

      // Should show empty state
      await expect(page.locator('text=No workspaces yet')).toBeVisible({ timeout: 5000 })
    })

    test('should have search input', async ({ page }) => {
      await page.route('**/api/workspaces*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: mockResponses.workspaces,
            totalCount: mockResponses.workspaces.length,
          }),
        })
      })

      await page.goto('/workspaces')

      // Search input should be visible
      const searchInput = page.locator('input[placeholder*="Search workspaces"]')
      await expect(searchInput).toBeVisible()
    })
  })

  test.describe('Workspace CRUD', () => {
    test('should have new workspace button', async ({ page }) => {
      await page.route('**/api/workspaces*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: mockResponses.workspaces,
            totalCount: mockResponses.workspaces.length,
          }),
        })
      })

      await page.goto('/workspaces')

      // New Workspace button should be visible
      const newButton = page.locator('button:has-text("New Workspace")')
      await expect(newButton).toBeVisible()
    })

    test('should open create dialog when clicking new workspace', async ({ page }) => {
      await page.route('**/api/workspaces*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: mockResponses.workspaces,
            totalCount: mockResponses.workspaces.length,
          }),
        })
      })

      await page.goto('/workspaces')

      // Click new workspace button
      await page.click('button:has-text("New Workspace")')

      // Dialog should open
      await expect(page.locator('text=Create New Workspace')).toBeVisible({ timeout: 5000 })
    })

    test('should have form fields in create dialog', async ({ page }) => {
      await page.route('**/api/workspaces*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [],
            totalCount: 0,
          }),
        })
      })

      await page.goto('/workspaces')

      // Click new workspace button
      await page.click('button:has-text("New Workspace")')

      // Form fields should be visible
      await expect(page.locator('input[placeholder="My Workspace"]')).toBeVisible({ timeout: 5000 })
      await expect(page.locator('textarea[placeholder*="What is this workspace for"]')).toBeVisible()
    })

    test('should navigate to workspace when clicking card', async ({ page }) => {
      await page.route('**/api/workspaces*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: mockResponses.workspaces,
            totalCount: mockResponses.workspaces.length,
          }),
        })
      })

      await page.goto('/workspaces')

      // Wait for workspaces to load
      await expect(page.locator('text=Test Workspace')).toBeVisible({ timeout: 5000 })

      // Click on workspace card
      await page.click('.card:has-text("Test Workspace")')

      // Should navigate to workspace detail
      await expect(page).toHaveURL(/\/workspaces\/workspace-1/)
    })
  })

  test.describe('Workspace Cards', () => {
    test('should display document count', async ({ page }) => {
      await page.route('**/api/workspaces*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [
              {
                ...mockResponses.workspaces[0],
                stats: { documentCount: 15, queryCount: 42 },
              },
            ],
            totalCount: 1,
          }),
        })
      })

      await page.goto('/workspaces')

      // Should show document count
      await expect(page.locator('text=15 documents')).toBeVisible({ timeout: 5000 })
    })

    test('should display query count', async ({ page }) => {
      await page.route('**/api/workspaces*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [
              {
                ...mockResponses.workspaces[0],
                stats: { documentCount: 15, queryCount: 42 },
              },
            ],
            totalCount: 1,
          }),
        })
      })

      await page.goto('/workspaces')

      // Should show query count
      await expect(page.locator('text=42 queries')).toBeVisible({ timeout: 5000 })
    })

    test('should display workspace description', async ({ page }) => {
      await page.route('**/api/workspaces*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [
              {
                ...mockResponses.workspaces[0],
                description: 'A test workspace for testing',
              },
            ],
            totalCount: 1,
          }),
        })
      })

      await page.goto('/workspaces')

      // Should show description
      await expect(page.locator('text=A test workspace for testing')).toBeVisible({ timeout: 5000 })
    })

    test('should show menu on hover', async ({ page }) => {
      await page.route('**/api/workspaces*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: mockResponses.workspaces,
            totalCount: mockResponses.workspaces.length,
          }),
        })
      })

      await page.goto('/workspaces')

      // Wait for workspaces to load
      await expect(page.locator('text=Test Workspace')).toBeVisible({ timeout: 5000 })

      // Hover over the workspace card to reveal menu
      await page.hover('.card:has-text("Test Workspace")')

      // Menu button should become visible
      const menuButton = page.locator('.card:has-text("Test Workspace") button:has(svg)')
      await expect(menuButton.first()).toBeVisible()
    })
  })

  test.describe('Workspace Header', () => {
    test('should display page title', async ({ page }) => {
      await page.route('**/api/workspaces*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [],
            totalCount: 0,
          }),
        })
      })

      await page.goto('/workspaces')

      // Should show page title
      await expect(page.locator('h1:text("Workspaces")')).toBeVisible()
    })

    test('should display page description', async ({ page }) => {
      await page.route('**/api/workspaces*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [],
            totalCount: 0,
          }),
        })
      })

      await page.goto('/workspaces')

      // Should show page description
      await expect(page.locator('text=Manage your knowledge bases')).toBeVisible()
    })
  })
})
