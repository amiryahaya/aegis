# AEGIS - Agentic Entity & Graph Intelligence System

A comprehensive Retrieval-Augmented Generation (RAG) system designed for intelligence domain applications, built on the .NET 10 technology stack with agentic reasoning capabilities.

> **AEGIS** - *Agentic Entity & Graph Intelligence System*

## Overview

AEGIS is a single-tenant, multi-user platform that provides:

- **Workspaces** - Organize investigations with shared context across multiple conversations
- **Team-based Access Control** - Data source access scoped by team with administrator controls
- **Hybrid Retrieval** - Combined vector similarity and BM25 keyword search
- **Knowledge Graph Integration** - Entity relationship analysis using Neo4j
- **Agentic Reasoning** - Planning and multi-agent orchestration via Microsoft Semantic Kernel
- **Comprehensive Audit Logging** - Full compliance and traceability features

## Latest Updates ✨

### Sprint 123-124: Data Source Management & Sync Monitoring ✅ (January 2026)
- **DataSourceConfigDialog** - Type-specific configuration for 8+ data source types
- **DataSourceDetailsDrawer** - Overview & sync history with statistics
- **WorkspaceDetailView** - Enhanced Data Sources tab with grid layout, quick actions
- **Sync Monitoring** - Track sync operations, success rates, document counts

### Sprint 121-122: Document Management Enhancement ✅ (January 2026)
- **DocumentManagerPanel** - Search, filter, sort, bulk select/delete documents
- **DocumentDetailsDrawer** - Slide-out drawer with metadata, chunks, actions
- **DocumentUploadDialog** - Drag-drop upload, multi-file, progress tracking
- **WorkspaceDetailView** - Integrated new document management components

### Sprint 119-120: API Key Management & Account Settings ✅ (January 2026)
- **API Key Management** - Create, list, revoke keys with scopes and expiration
- **Account Settings** - Data export (JSON), account deletion with confirmation
- **Settings View** - 7 tabs (Profile, Appearance, Notifications, Privacy, Security, API Keys, Account)

### Sprint 117-118: Security Settings & Account Management ✅ (January 2026)
- **Security Settings** - Password change, MFA setup, login activity
- **Active Sessions** - View and revoke active sessions across devices
- **Two-Factor Auth** - TOTP setup with QR code and backup codes

### Sprint 115-116: Password Recovery & Error Pages ✅ (January 2026)
- **Forgot Password Flow** - Email-based password reset request with validation
- **Reset Password Flow** - Token validation, password strength indicator, auto-redirect
- **404 Not Found Page** - Auth-aware navigation with helpful quick links

### Vue 3 Frontend (Sprint 51-124) ✅
Complete Vue 3 + TailwindCSS frontend with:
- **Authentication** - Login, register, forgot/reset password, MFA, 404 page
- **Chat Interface** - SignalR streaming with source citations
- **Session & Workspace Management** - CRUD, export, filtering, bulk operations
- **Document Management** - Upload, search, filter, bulk actions, details drawer
- **Data Source Management** - Type-specific config, sync monitoring, history tracking
- **Admin Dashboard** - System health, metrics, user management
- **Global Search** - Full-text search with filters and document preview
- **User Profile** - Profile management, API keys, security settings
- **Activity & Collaboration** - Activity feed, comments, reactions, presence
- **PWA Support** - Installable app with offline caching
- **Error Handling** - Toast notifications, error boundaries, ARIA support
- **Testing** - 76 unit tests, 70+ E2E tests with Playwright

### UUID v7 Migration (December 2025)
- **Time-Ordered Identifiers** - All entities use UUID v7 for better database performance
- **PostgreSQL 18** - Upgraded to latest PostgreSQL with native optimizations
- **30-50% Faster Inserts** - Sequential UUIDs reduce B-tree index fragmentation
- **Database-Friendly** - UUIDNext library generates PostgreSQL-optimized IDs

### Sprint 17-18: Data Connectors & RAG Improvements

#### Data Connectors & Sync
- **PostgreSQL Connector** - Sync table data with incremental updates
- **MongoDB Connector** - Ingest MongoDB collections with BSON conversion
- **RSS Feed Connector** - Automated news and content ingestion
- **Hangfire Scheduler** - Background sync jobs with cron scheduling
- **Sync History** - Track sync operations, status, and metrics

#### RAG Enhancements
- **Cross-Encoder Reranking** - Improve relevance with Cohere API (with fallback)
- **Query History** - Track all queries with full-text search and analytics
- **User Feedback** - Collect ratings (positive/negative/neutral) with comments
- **Enhanced Context Assembly** - Optional reranking pipeline for better results

**New API Endpoints:**
- `POST/GET /api/sync/*` - Data source synchronization
- `POST/GET /api/query-history/*` - Query tracking and search
- `POST/PUT/DELETE /api/feedback/*` - Feedback collection and statistics

See [Data Connectors Guide](./docs/DATA_CONNECTORS.md) and [Query Features Guide](./docs/QUERY_FEATURES.md) for details.

### Sprint 19-20: Semantic Kernel Integration ✅

#### Semantic Kernel Plugins (6 plugins, 21 tests passing)
- **VectorSearchPlugin** - Semantic similarity search using vector embeddings
- **KeywordSearchPlugin** - BM25 keyword-based search with term matching
- **GraphQueryPlugin** - Knowledge graph queries (entity networks, path finding)
- **EntityLookupPlugin** - Entity resolution and search by type
- **SanctionsCheckPlugin** - Entity verification against watchlists
- **TimelineBuilderPlugin** - Temporal timeline construction for entities

#### LLM & Authorization
- **OllamaLLMService** - Local LLM inference (non-streaming, RAG, streaming)
- **PluginAuthorizationService** - Role-based plugin access control
  - Viewer: VectorSearch, KeywordSearch
  - Contributor+: Graph queries, Entity lookup
  - Analyst+: Sanctions check, Timeline builder

#### Infrastructure Services
- **SemanticSearchService** - Combines embedding + vector store
- **KeywordSearchService** - Placeholder for Elasticsearch integration
- **SemanticKernelService** - Orchestrates all 6 plugins with DI

**Test Coverage:** 30 new passing tests (21 plugin + 6 authorization + 3 Ollama)

### Sprint 21-22: Planning & Orchestration ✅

#### Multi-Agent Orchestration (12 tests passing)
- **PlannerAgent** - Decomposes queries into executable task DAGs
- **TaskExecutor** - Executes plans with dependency management
- **RetrieverAgent** - Semantic search and information retrieval
- **AnalyzerAgent** - LLM-based analysis of retrieved data
- **SynthesizerAgent** - Final response synthesis from all results

#### Working Memory
- **WorkingMemoryService** - Session and conversation context management
  - In-memory key-value storage with expiration
  - Conversation history tracking
  - Multi-turn dialogue support

#### Key Features
- Rule-based task decomposition (search, entity, analysis, temporal)
- DAG-based execution with topological sort
- Automatic dependency resolution
- Result passing between agents
- Confidence scoring and reasoning traces

**Test Coverage:** 12 new passing tests (7 planner + 5 executor)

---

## Core Concepts

### Workspaces

Workspaces are containers for multiple conversations with shared, curated knowledge. They enable team collaboration and persistent context across investigation sessions.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                             WORKSPACE                                    │
│  "Operation Sentinel"                                                    │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                    WORKSPACE KNOWLEDGE BASE                      │   │
│  │  - Curated Entities (persons, organizations, locations)         │   │
│  │  - Key Findings (conclusions, hypotheses, evidence)             │   │
│  │  - Established Facts (verified statements)                       │   │
│  │  - Custom Instructions (workspace-specific guidelines)          │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                          │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                      CONVERSATIONS                               │   │
│  │  [Analyst A] Financial Investigation    ← shares workspace context│   │
│  │  [Analyst A] Network Mapping            ← shares workspace context│   │
│  │  [Analyst B] Timeline Analysis          ← shares workspace context│   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

#### Key Features

| Feature | Description |
|---------|-------------|
| **Persistent Knowledge** | Curated entities, findings, and facts persist across all conversations |
| **Multi-Conversation** | Start fresh conversations without losing workspace context |
| **Team Collaboration** | Multiple analysts share workspace knowledge, each with own conversations |
| **Knowledge Curation** | Promote discovered entities/facts from conversations to workspace level |
| **Custom Instructions** | Workspace-specific guidelines injected into every query |
| **Scoped Data Sources** | Restrict document retrieval to workspace-relevant sources |

#### Context Hierarchy

Every query in AEGIS is enriched with layered context:

```
1. Workspace Context (~4000 tokens)
   ├── Custom Instructions
   ├── Curated Entities
   ├── Key Findings
   └── Established Facts

2. Conversation Context (dynamic)
   ├── Recent Messages (last 5 turns verbatim)
   └── Summarized History (older turns compressed)

3. Retrieved Documents
   └── Scoped to workspace data sources
```

### Conversations

Conversations are individual chat threads within a workspace. Each conversation:
- Inherits workspace knowledge automatically
- Maintains its own memory (summarized over time)
- Can extract entities/facts for promotion to workspace level
- Supports endless context through progressive summarization

## Architecture Principles

### Design Philosophy
- **Vertical-Slice Architecture** - Features organized by business capability, not technical layers
- **Test-Driven Development (TDD)** - Tests written before implementation
- **Result Pattern** - Explicit success/failure handling without exceptions for control flow
- **Problem Details (RFC 7807)** - Standardized error responses across all APIs
- **CQRS** - Command Query Responsibility Segregation for complex domains

### Solution Structure (Vertical-Slice)
```
Aegis.sln
├── src/
│   ├── Aegis.Api/                    # API Host (Carter modules)
│   │   ├── Features/
│   │   │   ├── Authentication/
│   │   │   │   ├── Login/
│   │   │   │   │   ├── LoginEndpoint.cs
│   │   │   │   │   ├── LoginCommand.cs
│   │   │   │   │   ├── LoginCommandHandler.cs
│   │   │   │   │   └── LoginValidator.cs
│   │   │   │   └── Register/
│   │   │   ├── Workspaces/
│   │   │   │   ├── Create/
│   │   │   │   ├── Knowledge/
│   │   │   │   │   ├── PromoteEntity/
│   │   │   │   │   ├── AddFinding/
│   │   │   │   │   └── ExtractSuggestions/
│   │   │   │   └── Conversations/
│   │   │   ├── Documents/
│   │   │   │   ├── Upload/
│   │   │   │   ├── Process/
│   │   │   │   └── Search/
│   │   │   ├── Query/
│   │   │   │   ├── Ask/
│   │   │   │   ├── History/
│   │   │   │   └── Feedback/
│   │   │   ├── Agents/
│   │   │   ├── Graph/
│   │   │   └── Admin/
│   │   ├── Common/
│   │   │   ├── Behaviors/               # MediatR pipeline behaviors
│   │   │   ├── Errors/                  # Problem Details & error handling
│   │   │   └── Extensions/
│   │   └── Program.cs
│   │
│   ├── Aegis.Domain/                 # Domain entities, value objects
│   │   ├── Entities/
│   │   ├── ValueObjects/
│   │   ├── Errors/                      # Domain-specific errors
│   │   └── Common/
│   │       └── Result.cs                # Result<T> pattern
│   │
│   ├── Aegis.Infrastructure/         # External concerns
│   │   ├── Persistence/
│   │   │   ├── Repositories/            # Dapper repositories
│   │   │   ├── Migrations/              # DbUp or FluentMigrator
│   │   │   └── ConnectionFactory.cs
│   │   ├── Services/
│   │   │   ├── VectorStore/             # Qdrant client
│   │   │   ├── GraphDb/                 # Neo4j client
│   │   │   ├── Search/                  # Elasticsearch client
│   │   │   ├── LLM/                     # Semantic Kernel
│   │   │   └── Messaging/               # RabbitMQ/MassTransit
│   │   └── Observability/
│   │       ├── Logging/
│   │       ├── Metrics/
│   │       └── Tracing/
│   │
│   ├── Aegis.Web/                    # User Portal (React + TypeScript)
│   │   ├── src/
│   │   │   ├── components/           # UI components (shadcn/ui)
│   │   │   ├── features/             # Feature modules
│   │   │   ├── hooks/                # Custom hooks (SignalR, streaming)
│   │   │   └── lib/                  # API clients, utilities
│   │   └── tests/
│   │
│   ├── Aegis.Admin/                  # Admin Portal (React + TypeScript)
│   │   ├── src/
│   │   │   ├── components/           # Admin UI components
│   │   │   ├── pages/                # Admin pages
│   │   │   └── hooks/                # Admin hooks
│   │   └── tests/
│   │
│   └── Aegis.AppHost/                # .NET Aspire Host
│
├── tests/
│   ├── Aegis.UnitTests/
│   │   └── Features/                    # Mirror of API features
│   ├── Aegis.IntegrationTests/
│   │   ├── Features/
│   │   └── Fixtures/                    # Test containers, fixtures
│   ├── Aegis.ArchitectureTests/      # Architecture rule tests
│   └── Aegis.E2ETests/
│
├── docker/
│   ├── docker-compose.yml               # Development environment (with profiles)
│   ├── docker-compose.override.yml      # Local overrides
│   ├── init-scripts/                    # Database initialization
│   ├── prometheus.yml                   # Prometheus configuration
│   ├── grafana/                         # Grafana dashboards
│   ├── .env.example                     # Environment variables template
│   └── Makefile                         # Common commands
│
└── docs/
```

## Technology Stack

### Core Framework
| Component | Technology | Purpose |
|-----------|------------|---------|
| Runtime | .NET 10 LTS | Latest LTS with performance improvements |
| Minimal API | Carter | Lightweight, modular endpoint definitions |
| Mediator | MediatR | CQRS, decoupled handlers |
| Validation | FluentValidation | Request validation pipeline |
| Real-time | SignalR | WebSocket for streaming responses |
| Background Jobs | .NET BackgroundService + Hangfire | Ingestion pipelines, scheduled tasks |
| API Gateway | YARP | Routing, rate limiting, load balancing |
| Cloud-Native | .NET Aspire | Service orchestration, observability |

### Data Access & Patterns
| Component | Technology | Purpose |
|-----------|------------|---------|
| Micro-ORM | Dapper | High-performance SQL queries |
| Migrations | DbUp | Database versioning |
| **UUID Generation** | **UUIDNext** | **Time-ordered UUID v7 for better index performance** |
| Result Pattern | Custom | Explicit error handling |
| Problem Details | RFC 7807 implementation | Standardized API errors |

### AI/ML Components
| Component | Technology | Purpose |
|-----------|------------|---------|
| AI Orchestration | Microsoft Semantic Kernel 2.x | LLM integration, plugins, planning |
| ML Operations | ML.NET 4.x | NER, sentiment analysis, classification |
| ONNX Runtime | Microsoft.ML.OnnxRuntime | Local model inference (embeddings) |
| LLM Inference | LLamaSharp / Ollama API | Local LLM serving (Qwen2.5) |

### Data Layer
| Component | Technology | Purpose |
|-----------|------------|---------|
| Primary DB | **PostgreSQL 18** | Relational data with Dapper, **UUID v7 support** |
| Vector DB | Qdrant | Vector similarity search |
| Graph DB | Neo4j | Knowledge graph queries |
| Caching | Redis | Distributed caching, sessions |
| Search | Elasticsearch | Full-text search (BM25) |
| Message Queue | MassTransit + RabbitMQ | Async processing, events |

### Observability Stack
| Component | Technology | Purpose |
|-----------|------------|---------|
| Structured Logging | Serilog | JSON logs with context |
| Log Aggregation | Seq / Loki | Centralized log search |
| Metrics | OpenTelemetry + Prometheus | Performance metrics |
| Tracing | OpenTelemetry + Jaeger | Distributed tracing |
| Dashboards | Grafana | Visualization |
| Health Checks | AspNetCore.HealthChecks | Service health monitoring |
| Audit Trail | Custom audit tables | Compliance & debugging |

### Testing Stack
| Component | Technology | Purpose |
|-----------|------------|---------|
| Unit Testing | xUnit | Test framework |
| Mocking | NSubstitute | Mock dependencies |
| Assertions | FluentAssertions | Readable assertions |
| Integration Tests | Testcontainers | Real database testing |
| Architecture Tests | NetArchTest | Enforce architecture rules |
| API Testing | Alba | HTTP endpoint testing |
| Coverage | Coverlet | Code coverage reports |

### Frontend (Vue 3 + TailwindCSS)
| Component | Technology | Purpose |
|-----------|------------|---------|
| Web UI | Vue 3 + Vite 5 | Interactive web application |
| UI Components | Headless UI + TailwindCSS | Accessible component library |
| State Management | Pinia | Reactive state stores |
| Routing | Vue Router 4 | SPA navigation with guards |
| Real-time | SignalR | WebSocket streaming |
| Icons | Heroicons | SVG icon library |
| HTTP Client | Axios | API communication |

**Frontend Features (Sprint 51-122):**
- **Authentication** - Login, register, forgot/reset password, MFA, 404 page
- **Chat Interface** - Streaming responses with source citations
- **Session Management** - CRUD, export, filtering, bulk operations
- **Workspace Management** - Data sources, document management with search/filter/bulk actions
- **Document Management** - Upload dialog, details drawer, chunk viewer, reindex/download
- **Global Search** - Full-text search with advanced filters
- **Admin Dashboard** - System health, metrics, user management
- **User Profile** - API key management, security settings, account management
- **Activity Feed** - Threaded comments, reactions, mentions
- **Real-time Features** - Presence indicators, typing status
- **Accessibility** - Keyboard shortcuts, focus traps, ARIA support
- **PWA Support** - Installable with offline caching
- **Testing** - 76 unit tests, 70+ E2E tests

## Key Patterns

### Result Pattern
```csharp
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public Error? Error { get; }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);
}

// Usage in handler
public async Task<Result<QueryResponse>> Handle(AskQuery query)
{
    var documents = await _retriever.SearchAsync(query.Text);
    if (documents.IsFailure)
        return Result<QueryResponse>.Failure(documents.Error);

    var response = await _llm.GenerateAsync(query.Text, documents.Value);
    return Result<QueryResponse>.Success(response);
}
```

### Problem Details Response
```json
{
  "type": "https://aegis.dev/errors/validation-error",
  "title": "Validation Error",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/api/documents/upload",
  "traceId": "00-abc123-def456-00",
  "errors": {
    "file": ["File size exceeds maximum allowed (50MB)"],
    "dataSourceId": ["Data source not found"]
  }
}
```

### Carter Module Example
```csharp
public class QueryModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/query")
            .WithTags("Query")
            .RequireAuthorization();

        group.MapPost("/ask", HandleAsk)
            .WithName("AskQuery")
            .Produces<QueryResponse>(200)
            .ProducesProblem(400)
            .ProducesProblem(500);
    }

    private static async Task<IResult> HandleAsk(
        AskQueryRequest request,
        ISender sender,
        CancellationToken ct)
    {
        var result = await sender.Send(new AskQuery(request.Query), ct);
        return result.Match(
            success => Results.Ok(success),
            error => error.ToProblemDetails());
    }
}
```

### Dapper Repository Example
```csharp
public class DocumentRepository : IDocumentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public async Task<Result<Document>> GetByIdAsync(Guid id)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();

        var document = await connection.QuerySingleOrDefaultAsync<Document>(
            """
            SELECT id, file_name, content_hash, chunk_count, status, processed_at
            FROM documents
            WHERE id = @Id
            """,
            new { Id = id });

        return document is not null
            ? Result<Document>.Success(document)
            : Result<Document>.Failure(DocumentErrors.NotFound(id));
    }
}
```

## Prerequisites

### Development Environment
| Component | Minimum | Recommended |
|-----------|---------|-------------|
| CPU | 8 cores | 16 cores |
| RAM | 32GB | 64GB |
| GPU | RTX 3080 (10GB) | RTX 4090 (24GB) |
| Storage | 500GB NVMe | 1TB NVMe |

### Required Software
- .NET 10 SDK
- Docker Desktop
- Git

## Quick Start

### 1. Clone the Repository
```bash
git clone https://github.com/your-org/aegis.git
cd aegis
```

### 2. Start Infrastructure Services

```bash
# Copy environment template
cp docker/.env.example docker/.env

# Start core services (PostgreSQL, Redis, Qdrant, Neo4j)
make up

# Or start all services
make up-full

# Or start with observability stack
make up-all
```

**Docker Compose Profiles:**

| Profile | Command | Services |
|---------|---------|----------|
| (default) | `make up` | **PostgreSQL 18** + pgvector, Redis 7, Qdrant, **Neo4j 5** |
| `full` | `make up-full` | + Elasticsearch, RabbitMQ, MinIO, Ollama |
| `observability` | `make up-obs` | + Seq, Jaeger, Prometheus, Grafana |
| `gpu` | `make up-gpu` | Ollama with NVIDIA GPU support |

### 3. Run Database Migrations
```bash
dotnet run --project src/Aegis.Api -- migrate
```

### 4. Run the Application
```bash
dotnet run --project src/Aegis.AppHost
```

### 5. Access the Application
| Service | URL | Credentials |
|---------|-----|-------------|
| Web UI | https://localhost:5001 | - |
| API | https://localhost:5000 | - |
| **Neo4j Browser** | http://localhost:7474 | neo4j / password |
| Seq Logs | http://localhost:5341 | - |
| Grafana | http://localhost:3000 | admin / admin |
| Jaeger Tracing | http://localhost:16686 | - |
| RabbitMQ Management | http://localhost:15672 | guest / guest |

## Development Workflow (TDD)

### Red-Green-Refactor Cycle
```bash
# 1. Write failing test
dotnet test tests/Aegis.UnitTests --filter "FullyQualifiedName~DocumentUpload"

# 2. Implement minimum code to pass
# 3. Refactor while keeping tests green

# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Test Categories
```bash
# Unit tests (fast, no external dependencies)
dotnet test tests/Aegis.UnitTests

# Integration tests (requires Docker)
make test-integration

# Architecture tests
dotnet test tests/Aegis.ArchitectureTests

# E2E tests
dotnet test tests/Aegis.E2ETests
```

## Observability

### Structured Logging
```csharp
Log.Information(
    "Query processed {@QueryId} in {Duration}ms with {DocumentCount} sources",
    queryId, duration, documents.Count);
```

### Correlation & Tracing
- All requests include `X-Correlation-Id` header
- OpenTelemetry traces span across services
- Audit logs link to trace IDs for debugging

### Audit Trail
All sensitive operations are logged:
- User authentication events
- Document uploads/deletions
- Query executions
- Admin configuration changes
- Data source access grants

## Configuration

### Environment Variables
```bash
# Database
POSTGRES_CONNECTION="Host=localhost;Database=aegis;Username=postgres;Password=postgres"

# Vector Store
QDRANT_HOST="localhost"
QDRANT_PORT="6333"

# Graph Database
NEO4J_URI="bolt://localhost:7687"
NEO4J_USER="neo4j"
NEO4J_PASSWORD="password"

# Redis
REDIS_CONNECTION="localhost:6379"

# Elasticsearch
ELASTICSEARCH_URL="http://localhost:9200"

# LLM
OLLAMA_URL="http://localhost:11434"
LLM_MODEL="qwen2.5:32b"

# Observability
SEQ_URL="http://localhost:5341"
JAEGER_ENDPOINT="http://localhost:14268/api/traces"
```

## API Overview

### Workspaces

#### Create Workspace
```http
POST /api/v1/workspaces
Content-Type: application/json

{
  "name": "Operation Sentinel",
  "description": "Investigation into network of interest",
  "teamId": "uuid",
  "customInstructions": "Focus on financial connections and timeline analysis"
}
```

#### Create Conversation in Workspace
```http
POST /api/v1/workspaces/{workspaceId}/conversations
Content-Type: application/json

{
  "title": "Financial Trail Analysis"
}
```

#### Promote Entity to Workspace Knowledge
```http
POST /api/v1/workspaces/{workspaceId}/knowledge/entities
Content-Type: application/json

{
  "name": "John Smith",
  "type": "Person",
  "description": "Primary subject of investigation",
  "aliases": ["J. Smith", "JS"],
  "confidence": "High",
  "sourceConversationId": "uuid"
}
```

#### Add Finding to Workspace
```http
POST /api/v1/workspaces/{workspaceId}/knowledge/findings
Content-Type: application/json

{
  "title": "Financial connection confirmed",
  "content": "Evidence shows direct wire transfers between Entity A and Entity B",
  "type": "Evidence",
  "supportingEntityIds": ["uuid1", "uuid2"],
  "sourceDocumentIds": ["doc1", "doc2"]
}
```

#### Extract Suggestions from Conversation
```http
POST /api/v1/workspaces/{workspaceId}/conversations/{conversationId}/extract

Response:
{
  "entities": [
    {"name": "Acme Corp", "type": "Organization", "confidence": "High"}
  ],
  "facts": [
    {"statement": "Acme Corp registered in 2019", "confidence": "Confirmed"}
  ],
  "relationships": [
    {"entity1": "John Smith", "relationship": "CEO_OF", "entity2": "Acme Corp"}
  ]
}
```

### Query Endpoint
```http
POST /api/v1/workspaces/{workspaceId}/conversations/{conversationId}/ask
Content-Type: application/json
X-Correlation-Id: abc-123

{
  "query": "What connections exist between Entity A and Entity B?",
  "options": {
    "useGraphRetrieval": true,
    "maxResults": 10
  }
}
```

### Error Response (Problem Details)
```http
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json

{
  "type": "https://aegis.dev/errors/validation-error",
  "title": "Validation Error",
  "status": 400,
  "detail": "Query text is required",
  "instance": "/api/v1/workspaces/{workspaceId}/conversations/{conversationId}/ask",
  "traceId": "00-abc123-def456-00"
}
```

## Web Portals

### User Portal (`Aegis.Web`)

A modern, responsive React application for end-users to interact with AEGIS.

| Feature | Description |
|---------|-------------|
| Chat Interface | Real-time streaming responses with citations |
| Workspaces | Create and manage investigation workspaces |
| Knowledge Browser | Browse curated entities, findings, and facts |
| Conversation History | Full conversation history with search |
| Source Panel | View cited documents and sources |
| Export | Export conversations to Markdown, PDF, Word, HTML, JSON |

**Technology Stack:**
- React 18+ with TypeScript
- TanStack Query + Zustand (state management)
- shadcn/ui + Radix UI (components)
- Tailwind CSS (styling)
- SignalR (real-time streaming)
- Vite (build tool)

**Key Routes:**
| Route | Description |
|-------|-------------|
| `/workspaces` | List and manage workspaces |
| `/workspaces/:id` | Workspace detail and conversations |
| `/workspaces/:id/c/:conversationId` | Chat interface |
| `/workspaces/:id/knowledge` | Knowledge base browser |

### Admin Portal (`Aegis.Admin`)

A comprehensive administrative interface for system management.

| Feature | Description |
|---------|-------------|
| Dashboard | System health, usage stats, alerts |
| User Management | Create, edit, delete users and roles |
| Data Sources | Configure and manage document connectors |
| Document Management | Monitor ingestion queue, reprocess documents |
| Analytics | Query volume, user activity, performance metrics |
| System Configuration | LLM settings, cache management, security |
| Audit Logs | Complete audit trail and security events |

**Key Routes:**
| Route | Description |
|-------|-------------|
| `/admin` | Dashboard overview |
| `/admin/users` | User management |
| `/admin/data-sources` | Data source connectors |
| `/admin/documents` | Document management |
| `/admin/analytics` | Usage analytics |
| `/admin/system` | System configuration |
| `/admin/audit` | Audit log viewer |

**User Roles:**
| Role | Permissions |
|------|-------------|
| Viewer | Read-only access to workspaces and conversations |
| Contributor | Participate in conversations, add annotations and comments |
| Analyst | Create workspaces, curate knowledge, manage findings |
| Admin | Manage users, teams, data sources |
| SystemAdmin | System configuration, SSO setup, organization settings |

## Export Formats

Export conversations and workspaces to multiple formats for sharing and archiving.

| Format | Extension | Use Case |
|--------|-----------|----------|
| Markdown | `.md` | Developer-friendly, version control, documentation |
| PDF | `.pdf` | Formal reports, printing, sharing |
| Word | `.docx` | Editable documents, collaboration |
| HTML | `.html` | Web viewing, email embedding |
| JSON | `.json` | Data interchange, backup, integration |

**Export Options:**
- Include/exclude source citations
- Include/exclude message timestamps
- Include/exclude document metadata
- Include workspace knowledge base (entities, findings, facts)
- Custom document title and author
- Bulk export multiple conversations as ZIP archive

**API Endpoints:**
```http
POST /api/v1/export/conversations/{conversationId}
POST /api/v1/export/workspaces/{workspaceId}
POST /api/v1/export/bulk
```

## Enterprise Features

### Authentication & SSO

| Provider | Protocol |
|----------|----------|
| Azure AD / Entra ID | OIDC |
| Okta | OIDC |
| Keycloak | OIDC |
| Auth0 | OIDC |
| Google Workspace | OIDC |
| LDAP/Active Directory | LDAP |

- JWT-based authentication with refresh tokens
- External identity provider integration
- Session management and token revocation
- Password reset and account lockout protection

### Organization Settings

Centralized configuration for single-organization deployment.

**Organization Profile:**
- Company name, logo, and branding colors
- Support contact information
- Privacy policy and terms of service links

**System Settings:**
- LLM model configuration and limits
- Storage and document quotas
- Feature flags (web search, OCR, exports)
- Session timeout and security policies
- IP allowlist for restricted access

**Usage Tracking:**
- Query and token usage per user/team
- Estimated cost calculations per LLM model
- Active user and storage metrics
- Exportable usage reports

### Notifications & Webhooks

**Notification Channels:**
- In-App (SignalR real-time)
- Email (SendGrid/SMTP)
- Slack integration
- MS Teams integration
- Custom webhooks

**Webhook Features:**
- HMAC signature verification
- Automatic retry with exponential backoff
- Delivery history and debugging
- Event filtering

### Collaboration

- Workspace sharing (user, team, email invite, public link)
- Real-time presence (see who's online)
- Comments and @mentions on conversations/findings
- Threaded discussions
- Activity feed

### Data Retention & GDPR

**Compliance:**
- GDPR (Right to erasure, access, portability)
- CCPA disclosure requirements
- SOC 2 audit trails
- Configurable retention policies

**Privacy Features:**
- Data subject request handling (30-day SLA)
- Consent management
- Automatic data archival/deletion
- PII anonymization
- Complete audit logging

## Performance Targets

| Metric | Target |
|--------|--------|
| Query Response Time (P50) | < 3 seconds |
| Query Response Time (P95) | < 10 seconds |
| Retrieval Recall@10 | > 0.85 |
| Answer Faithfulness | > 0.85 |
| System Uptime | > 99.5% |
| Concurrent Users | > 50 |
| Test Coverage | > 80% |

## Documentation

### User Guides
- [Data Connectors Guide](./docs/DATA_CONNECTORS.md) - PostgreSQL, MongoDB, RSS feed setup
- [Query Features Guide](./docs/QUERY_FEATURES.md) - Query history, feedback, reranking
- [User Guide](./docs/user-guide.md) - End-user documentation

### Developer Documentation
- [Development Plan](./docs/DEVELOPMENT_PLAN.md) - Detailed implementation roadmap
- [API Documentation](./docs/api.md) - OpenAPI specification
- [Architecture Guide](./docs/architecture.md) - System design details
- [Testing Guide](./docs/testing.md) - TDD practices and conventions

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Write tests first (TDD)
4. Implement the feature
5. Ensure all tests pass (`dotnet test`)
6. Commit your changes (`git commit -m 'Add amazing feature'`)
7. Push to the branch (`git push origin feature/amazing-feature`)
8. Open a Pull Request

## License

This project is proprietary software. All rights reserved.

## Support

For support and questions, contact the development team.
