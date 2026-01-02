import { fileURLToPath, URL } from 'node:url'
import { defineConfig, splitVendorChunkPlugin, type PluginOption } from 'vite'
import vue from '@vitejs/plugin-vue'
import { VitePWA } from 'vite-plugin-pwa'
import { visualizer } from 'rollup-plugin-visualizer'

const isAnalyze = process.env.ANALYZE === 'true'

export default defineConfig({
  plugins: [
    vue(),
    splitVendorChunkPlugin(),
    // Bundle analyzer - only in analyze mode
    isAnalyze && visualizer({
      filename: 'dist/stats.html',
      open: true,
      gzipSize: true,
      brotliSize: true,
      template: 'treemap', // sunburst, treemap, network
    }) as PluginOption,
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['favicon.ico', 'robots.txt', 'apple-touch-icon.png'],
      manifest: {
        name: 'AEGIS - AI-Enhanced Knowledge System',
        short_name: 'AEGIS',
        description: 'Enterprise-grade RAG system for intelligent document retrieval and knowledge management',
        theme_color: '#3b82f6',
        background_color: '#111827',
        display: 'standalone',
        orientation: 'portrait-primary',
        scope: '/',
        start_url: '/',
        icons: [
          {
            src: '/icons/icon-72x72.png',
            sizes: '72x72',
            type: 'image/png',
            purpose: 'maskable any'
          },
          {
            src: '/icons/icon-96x96.png',
            sizes: '96x96',
            type: 'image/png',
            purpose: 'maskable any'
          },
          {
            src: '/icons/icon-128x128.png',
            sizes: '128x128',
            type: 'image/png',
            purpose: 'maskable any'
          },
          {
            src: '/icons/icon-144x144.png',
            sizes: '144x144',
            type: 'image/png',
            purpose: 'maskable any'
          },
          {
            src: '/icons/icon-152x152.png',
            sizes: '152x152',
            type: 'image/png',
            purpose: 'maskable any'
          },
          {
            src: '/icons/icon-192x192.png',
            sizes: '192x192',
            type: 'image/png',
            purpose: 'maskable any'
          },
          {
            src: '/icons/icon-384x384.png',
            sizes: '384x384',
            type: 'image/png',
            purpose: 'maskable any'
          },
          {
            src: '/icons/icon-512x512.png',
            sizes: '512x512',
            type: 'image/png',
            purpose: 'maskable any'
          }
        ],
        categories: ['productivity', 'business', 'utilities'],
        shortcuts: [
          {
            name: 'New Chat',
            short_name: 'Chat',
            description: 'Start a new chat session',
            url: '/chat',
            icons: [{ src: '/icons/chat-icon.png', sizes: '96x96' }]
          },
          {
            name: 'Search',
            short_name: 'Search',
            description: 'Search your knowledge base',
            url: '/search',
            icons: [{ src: '/icons/search-icon.png', sizes: '96x96' }]
          }
        ]
      },
      workbox: {
        // Cache strategies
        runtimeCaching: [
          {
            // Cache API responses with network-first strategy
            urlPattern: /^https:\/\/.*\/api\/.*/i,
            handler: 'NetworkFirst',
            options: {
              cacheName: 'api-cache',
              expiration: {
                maxEntries: 100,
                maxAgeSeconds: 60 * 60 // 1 hour
              },
              cacheableResponse: {
                statuses: [0, 200]
              }
            }
          },
          {
            // Cache static assets with cache-first strategy
            urlPattern: /\.(?:png|jpg|jpeg|svg|gif|webp|ico)$/i,
            handler: 'CacheFirst',
            options: {
              cacheName: 'images-cache',
              expiration: {
                maxEntries: 50,
                maxAgeSeconds: 60 * 60 * 24 * 30 // 30 days
              }
            }
          },
          {
            // Cache fonts
            urlPattern: /\.(?:woff|woff2|eot|ttf|otf)$/i,
            handler: 'CacheFirst',
            options: {
              cacheName: 'fonts-cache',
              expiration: {
                maxEntries: 20,
                maxAgeSeconds: 60 * 60 * 24 * 365 // 1 year
              }
            }
          },
          {
            // Cache Google Fonts
            urlPattern: /^https:\/\/fonts\.(?:gstatic|googleapis)\.com\/.*/i,
            handler: 'CacheFirst',
            options: {
              cacheName: 'google-fonts-cache',
              expiration: {
                maxEntries: 30,
                maxAgeSeconds: 60 * 60 * 24 * 365 // 1 year
              }
            }
          }
        ],
        // Pre-cache critical assets
        globPatterns: ['**/*.{js,css,html,ico,png,svg,woff2}'],
        // Clean up old caches
        cleanupOutdatedCaches: true,
        // Skip waiting
        skipWaiting: true,
        clientsClaim: true
      },
      devOptions: {
        enabled: false // Disable in development
      }
    })
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    }
  },
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true
      },
      '/hubs': {
        target: 'http://localhost:5000',
        changeOrigin: true,
        ws: true
      }
    }
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
    // Performance optimizations
    target: 'esnext',
    minify: 'esbuild',
    cssMinify: true,
    // Chunk splitting configuration
    rollupOptions: {
      output: {
        // Manual chunk splitting for better caching
        manualChunks: {
          // Vue core - rarely changes
          'vue-core': ['vue', 'vue-router', 'pinia'],
          // UI libraries
          'ui-libs': ['@headlessui/vue', '@heroicons/vue'],
          // SignalR for real-time
          'signalr': ['@microsoft/signalr'],
          // Utilities
          'utils': ['axios', 'date-fns', 'zod', 'marked'],
        },
        // Asset file naming for better caching
        chunkFileNames: 'assets/js/[name]-[hash].js',
        entryFileNames: 'assets/js/[name]-[hash].js',
        assetFileNames: (assetInfo) => {
          if (/\.(png|jpe?g|gif|svg|webp|ico)$/i.test(assetInfo.name || '')) {
            return 'assets/images/[name]-[hash][extname]'
          }
          if (/\.css$/i.test(assetInfo.name || '')) {
            return 'assets/css/[name]-[hash][extname]'
          }
          if (/\.(woff2?|eot|ttf|otf)$/i.test(assetInfo.name || '')) {
            return 'assets/fonts/[name]-[hash][extname]'
          }
          return 'assets/[name]-[hash][extname]'
        },
      },
    },
    // Chunk size warnings
    chunkSizeWarningLimit: 500,
    // Report compressed sizes
    reportCompressedSize: true,
  },
  // Optimize dependencies
  optimizeDeps: {
    include: [
      'vue',
      'vue-router',
      'pinia',
      '@headlessui/vue',
      '@heroicons/vue/24/outline',
      '@heroicons/vue/24/solid',
      '@microsoft/signalr',
      'axios',
      'date-fns',
      'zod',
      'marked',
    ],
  },
  // Enable CSS code splitting
  css: {
    devSourcemap: true,
  },
})
