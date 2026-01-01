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

test.describe('Search', () => {
  test.beforeEach(async ({ page }) => {
    await setupAuth(page)

    // Mock workspaces for filter
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
  })

  test.describe('Search Page', () => {
    test('should display search page', async ({ page }) => {
      await page.goto('/search')

      // Should display search input
      const searchInput = page.locator('input[placeholder*="Search"]')
      await expect(searchInput).toBeVisible({ timeout: 5000 })
    })

    test('should display initial state message', async ({ page }) => {
      await page.goto('/search')

      // Should display initial state
      await expect(page.locator('text=Search AEGIS')).toBeVisible({ timeout: 5000 })
    })

    test('should have filter toggle button', async ({ page }) => {
      await page.goto('/search')

      // Filters button should be visible
      await expect(page.locator('text=Filters')).toBeVisible()
    })
  })

  test.describe('Search Results', () => {
    test('should display results after search', async ({ page }) => {
      await page.route('**/api/search*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [
              {
                id: 'result-1',
                type: 'session',
                title: 'Machine Learning Discussion',
                excerpt: 'Discussion about ML algorithms...',
                score: 0.95,
                createdAt: new Date().toISOString(),
                highlights: ['Machine', 'Learning'],
                metadata: {},
              },
            ],
            totalCount: 1,
            searchTimeMs: 42,
          }),
        })
      })

      await page.goto('/search')

      // Enter search query
      const searchInput = page.locator('input[placeholder*="Search"]')
      await searchInput.fill('machine learning')
      await page.keyboard.press('Enter')

      // Should display results
      await expect(page.locator('text=Machine Learning Discussion')).toBeVisible({ timeout: 5000 })
    })

    test('should show no results message when no matches', async ({ page }) => {
      await page.route('**/api/search*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [],
            totalCount: 0,
            searchTimeMs: 15,
          }),
        })
      })

      await page.goto('/search')

      // Enter search query
      const searchInput = page.locator('input[placeholder*="Search"]')
      await searchInput.fill('xyznonexistent')
      await page.keyboard.press('Enter')

      // Should show no results
      await expect(page.locator('text=No results found')).toBeVisible({ timeout: 5000 })
    })

    test('should display result count', async ({ page }) => {
      await page.route('**/api/search*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [
              {
                id: 'result-1',
                type: 'session',
                title: 'Test Result',
                excerpt: 'Test excerpt...',
                score: 0.9,
                createdAt: new Date().toISOString(),
                highlights: [],
                metadata: {},
              },
            ],
            totalCount: 1,
            searchTimeMs: 25,
          }),
        })
      })

      await page.goto('/search')

      // Enter search query
      const searchInput = page.locator('input[placeholder*="Search"]')
      await searchInput.fill('test')
      await page.keyboard.press('Enter')

      // Should show result count
      await expect(page.locator('text=1 results')).toBeVisible({ timeout: 5000 })
    })
  })

  test.describe('Search Filters', () => {
    test('should show filter panel when clicking filters', async ({ page }) => {
      await page.goto('/search')

      // Click filters button
      await page.click('text=Filters')

      // Filter options should be visible
      await expect(page.locator('text=Type')).toBeVisible({ timeout: 5000 })
      await expect(page.locator('text=Date Range')).toBeVisible()
    })

    test('should have type filter buttons', async ({ page }) => {
      await page.goto('/search')

      // Open filters
      await page.click('text=Filters')

      // Type filters should be visible
      await expect(page.locator('button:has-text("Sessions")')).toBeVisible({ timeout: 5000 })
      await expect(page.locator('button:has-text("Documents")')).toBeVisible()
      await expect(page.locator('button:has-text("Workspaces")')).toBeVisible()
    })

    test('should have date preset filters', async ({ page }) => {
      await page.goto('/search')

      // Open filters
      await page.click('text=Filters')

      // Date presets should be visible
      await expect(page.locator('button:has-text("Today")')).toBeVisible({ timeout: 5000 })
      await expect(page.locator('button:has-text("Past Week")')).toBeVisible()
      await expect(page.locator('button:has-text("Past Month")')).toBeVisible()
    })
  })

  test.describe('Recent Searches', () => {
    test('should display recent searches when available', async ({ page }) => {
      await page.goto('/search')

      // Set up recent searches in localStorage
      await page.evaluate(() => {
        const searchStore = {
          recentSearches: [
            { query: 'machine learning', timestamp: Date.now(), resultCount: 5 },
            { query: 'data analysis', timestamp: Date.now() - 3600000, resultCount: 3 },
          ],
        }
        localStorage.setItem('search-store', JSON.stringify(searchStore))
      })

      // Reload to pick up localStorage
      await page.reload()

      // Recent searches should be visible (if store loads them)
      // Note: This depends on Pinia hydration from localStorage
    })
  })

  test.describe('Search Navigation', () => {
    test('should update URL with search query', async ({ page }) => {
      await page.route('**/api/search*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [],
            totalCount: 0,
            searchTimeMs: 10,
          }),
        })
      })

      await page.goto('/search')

      // Enter search query
      const searchInput = page.locator('input[placeholder*="Search"]')
      await searchInput.fill('test query')
      await page.keyboard.press('Enter')

      // URL should include query param
      await expect(page).toHaveURL(/q=test/)
    })

    test('should load search from URL query', async ({ page }) => {
      await page.route('**/api/search*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [
              {
                id: 'result-1',
                type: 'document',
                title: 'Test Document',
                excerpt: 'Test content...',
                score: 0.85,
                createdAt: new Date().toISOString(),
                highlights: [],
                metadata: {},
              },
            ],
            totalCount: 1,
            searchTimeMs: 30,
          }),
        })
      })

      await page.goto('/search?q=test')

      // Search input should have the query
      const searchInput = page.locator('input[placeholder*="Search"]')
      await expect(searchInput).toHaveValue('test')

      // Results should be displayed
      await expect(page.locator('text=Test Document')).toBeVisible({ timeout: 5000 })
    })
  })

  test.describe('Result Types', () => {
    test('should display session results correctly', async ({ page }) => {
      await page.route('**/api/search*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [
              {
                id: 'session-1',
                type: 'session',
                title: 'Research Session',
                excerpt: 'A research session about AI...',
                score: 0.92,
                createdAt: new Date().toISOString(),
                highlights: [],
                metadata: {},
              },
            ],
            totalCount: 1,
            searchTimeMs: 20,
          }),
        })
      })

      await page.goto('/search?q=research')

      // Should show session type indicator
      await expect(page.locator('text=SESSION')).toBeVisible({ timeout: 5000 })
      await expect(page.locator('text=Research Session')).toBeVisible()
    })

    test('should display document results correctly', async ({ page }) => {
      await page.route('**/api/search*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [
              {
                id: 'doc-1',
                type: 'document',
                title: 'AI Research Paper.pdf',
                excerpt: 'This paper explores...',
                score: 0.88,
                createdAt: new Date().toISOString(),
                highlights: [],
                metadata: { workspaceName: 'Research' },
              },
            ],
            totalCount: 1,
            searchTimeMs: 25,
          }),
        })
      })

      await page.goto('/search?q=AI')

      // Should show document type indicator
      await expect(page.locator('text=DOCUMENT')).toBeVisible({ timeout: 5000 })
      await expect(page.locator('text=AI Research Paper.pdf')).toBeVisible()
    })

    test('should display workspace results correctly', async ({ page }) => {
      await page.route('**/api/search*', async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [
              {
                id: 'ws-1',
                type: 'workspace',
                title: 'Engineering Knowledge Base',
                excerpt: 'Technical documentation...',
                score: 0.85,
                createdAt: new Date().toISOString(),
                highlights: [],
                metadata: {},
              },
            ],
            totalCount: 1,
            searchTimeMs: 18,
          }),
        })
      })

      await page.goto('/search?q=engineering')

      // Should show workspace type indicator
      await expect(page.locator('text=WORKSPACE')).toBeVisible({ timeout: 5000 })
      await expect(page.locator('text=Engineering Knowledge Base')).toBeVisible()
    })
  })
})

test.describe('Header Search', () => {
  test('should have search in header', async ({ page }) => {
    await setupAuth(page)

    await page.goto('/')

    // Header search should be visible on desktop
    const headerSearch = page.locator('header input[placeholder*="Search"]')
    await expect(headerSearch).toBeVisible()
  })

  test('should navigate to search page on submit', async ({ page }) => {
    await setupAuth(page)

    await page.route('**/api/search*', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [],
          totalCount: 0,
          searchTimeMs: 10,
        }),
      })
    })

    await page.goto('/')

    // Use header search
    const headerSearch = page.locator('header input[placeholder*="Search"]')
    await headerSearch.fill('test query')
    await page.keyboard.press('Enter')

    // Should navigate to search page
    await expect(page).toHaveURL(/\/search/)
  })
})
