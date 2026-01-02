# =============================================================================
# AEGIS Web Frontend - Production Dockerfile
# Multi-stage build optimized for performance and security
# =============================================================================

# -----------------------------------------------------------------------------
# Stage 1: Dependencies - Install npm packages with caching
# -----------------------------------------------------------------------------
FROM node:20-alpine AS deps

WORKDIR /app

# Install dependencies only when package files change
COPY package.json package-lock.json ./

# Use npm ci for reproducible builds
RUN npm ci --only=production=false

# -----------------------------------------------------------------------------
# Stage 2: Build - Compile TypeScript and bundle assets
# -----------------------------------------------------------------------------
FROM node:20-alpine AS build

WORKDIR /app

# Copy dependencies from deps stage
COPY --from=deps /app/node_modules ./node_modules

# Copy source files
COPY . .

# Set production environment
ENV NODE_ENV=production

# Build the application
RUN npm run build

# Verify build output
RUN ls -la dist/ && \
    du -sh dist/

# -----------------------------------------------------------------------------
# Stage 3: Production - Nginx with optimized configuration
# -----------------------------------------------------------------------------
FROM nginx:1.25-alpine AS production

# Install curl for healthcheck
RUN apk add --no-cache curl

# Remove default nginx config
RUN rm /etc/nginx/conf.d/default.conf

# Copy custom nginx config
COPY nginx.conf /etc/nginx/nginx.conf

# Copy built assets from build stage
COPY --from=build /app/dist /usr/share/nginx/html

# Create non-root user for security
RUN addgroup -g 1001 -S aegis && \
    adduser -S -D -H -u 1001 -s /sbin/nologin -G aegis aegis && \
    chown -R aegis:aegis /usr/share/nginx/html && \
    chown -R aegis:aegis /var/cache/nginx && \
    chown -R aegis:aegis /var/log/nginx && \
    touch /var/run/nginx.pid && \
    chown -R aegis:aegis /var/run/nginx.pid

# Switch to non-root user
USER aegis

# Expose port 80 (requires CAP_NET_BIND_SERVICE or port > 1024)
EXPOSE 80

# Health check endpoint
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -f http://localhost/health || exit 1

# Labels for container metadata
LABEL org.opencontainers.image.title="AEGIS Web Frontend" \
      org.opencontainers.image.description="Vue 3 + TailwindCSS frontend for AEGIS RAG system" \
      org.opencontainers.image.vendor="AEGIS" \
      org.opencontainers.image.source="https://github.com/aegis/aegis-web"

# Start nginx in foreground
CMD ["nginx", "-g", "daemon off;"]
