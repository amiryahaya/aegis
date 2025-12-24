.PHONY: up down logs ps clean migrate test build run health

# =============================================================================
# Docker Commands
# =============================================================================

# Default: start minimal services (postgres, redis, qdrant)
up:
	docker compose -f docker/docker-compose.yml up -d

# Start all services
up-full:
	docker compose -f docker/docker-compose.yml --profile full up -d

# Start with observability stack
up-obs:
	docker compose -f docker/docker-compose.yml --profile observability up -d

# Start everything including observability
up-all:
	docker compose -f docker/docker-compose.yml --profile full --profile observability up -d

# Start with GPU support for Ollama
up-gpu:
	docker compose -f docker/docker-compose.yml --profile gpu up -d

# Stop all services
down:
	docker compose -f docker/docker-compose.yml --profile full --profile observability --profile gpu down

# View logs
logs:
	docker compose -f docker/docker-compose.yml logs -f

# View specific service logs
logs-%:
	docker compose -f docker/docker-compose.yml logs -f $*

# Show running containers
ps:
	docker compose -f docker/docker-compose.yml ps -a

# Clean up volumes (WARNING: destroys data)
clean:
	docker compose -f docker/docker-compose.yml --profile full --profile observability --profile gpu down -v
	docker volume prune -f

# =============================================================================
# .NET Commands
# =============================================================================

# Restore packages
restore:
	dotnet restore

# Build solution
build:
	dotnet build --no-restore

# Run API locally
run:
	dotnet run --project src/Aegis.Api

# Run with watch mode
watch:
	dotnet watch run --project src/Aegis.Api

# =============================================================================
# Database Commands
# =============================================================================

# Run database migrations
migrate:
	dotnet run --project src/Aegis.Api -- migrate

# =============================================================================
# Test Commands
# =============================================================================

# Run all tests
test:
	dotnet test

# Run unit tests only
test-unit:
	dotnet test tests/Aegis.UnitTests

# Run integration tests (requires Docker)
test-integration:
	docker compose -f docker/docker-compose.yml up -d
	dotnet test tests/Aegis.IntegrationTests
	docker compose -f docker/docker-compose.yml down

# Run architecture tests
test-arch:
	dotnet test tests/Aegis.ArchitectureTests

# Run tests with coverage
test-coverage:
	dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
	@echo "Coverage reports available in ./coverage"

# =============================================================================
# Health & Utility Commands
# =============================================================================

# Health check all services
health:
	@echo "=== PostgreSQL ===" && docker exec aegis-postgres pg_isready -U postgres || true
	@echo "=== Redis ===" && docker exec aegis-redis redis-cli ping || true
	@echo "=== Qdrant ===" && curl -s http://localhost:6333/readyz || true

# Pull Ollama models
ollama-pull:
	docker exec aegis-ollama ollama pull llama3.2
	docker exec aegis-ollama ollama pull nomic-embed-text

# Create MinIO buckets
minio-init:
	docker exec aegis-minio mc alias set local http://localhost:9000 minioadmin minioadmin
	docker exec aegis-minio mc mb local/aegis-documents --ignore-existing
	docker exec aegis-minio mc mb local/aegis-exports --ignore-existing

# Format code
format:
	dotnet format

# Clean build artifacts
clean-build:
	dotnet clean
	rm -rf **/bin **/obj

# Full setup for new developers
setup: restore build up
	@echo "Setup complete! Run 'make run' to start the API"
