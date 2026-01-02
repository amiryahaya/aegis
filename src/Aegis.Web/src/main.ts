import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import i18n from './i18n'
import { errorTrackingPlugin } from './composables/useErrorTracking'
import './assets/styles/main.css'

const app = createApp(App)

// State management
app.use(createPinia())

// Internationalization
app.use(i18n)

// Routing
app.use(router)

// Error tracking (global error handler)
app.use(errorTrackingPlugin)

// Mount app
app.mount('#app')
