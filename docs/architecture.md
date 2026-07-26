# LinkPulse Architecture

## System overview

```mermaid
flowchart LR
    Client[API client] --> API[ASP.NET Core Minimal API]
    Visitor[Short URL visitor] --> API

    API --> Auth[JWT authentication]
    API --> RateLimiter[Rate limiting]
    API --> Cache[Redis cache]
    API --> Database[(PostgreSQL)]

    Cache --> Redirect[Redirect resolution]
    Database --> Redirect

    Redirect --> ClickEvent[Click event recording]
    ClickEvent --> Database

    Worker[Expiration cleanup worker] --> Database
    Worker --> Cache
```

## Redirect flow

```mermaid
sequenceDiagram
    participant Visitor
    participant API
    participant Redis
    participant PostgreSQL

    Visitor->>API: GET /{shortCode}
    API->>Redis: GET cached link

    alt Cache hit
        Redis-->>API: Target URL
    else Cache miss
        Redis-->>API: Missing key
        API->>PostgreSQL: Find active link
        PostgreSQL-->>API: Link
        API->>Redis: Cache link with TTL
    end

    API->>PostgreSQL: Save ClickEvent
    API-->>Visitor: 302 Found
```

## Cache invalidation

The Redis entry is removed when:

- the target URL is updated;
- a link is disabled;
- the expiration worker deactivates an expired link;
- an invalid or stale cached value is detected.

PostgreSQL remains the source of truth. Redis failure does not prevent redirects: the API falls back to PostgreSQL.

## Database model

```mermaid
erDiagram
    APPLICATION_USERS ||--o{ SHORT_LINKS : owns
    SHORT_LINKS ||--o{ CLICK_EVENTS : receives

    APPLICATION_USERS {
        uuid id PK
        varchar email
        varchar normalized_email UK
        varchar password_hash
        timestamptz created_at
    }

    SHORT_LINKS {
        uuid id PK
        uuid owner_id FK
        varchar short_code UK
        varchar target_url
        timestamptz created_at
        timestamptz expires_at
        boolean is_active
    }

    CLICK_EVENTS {
        bigint id PK
        uuid short_link_id FK
        timestamptz occurred_at
        varchar referrer
        varchar user_agent
        varchar client_ip_hash
    }
```

## Background processing

`ExpiredLinkCleanupWorker` periodically selects active links whose `ExpiresAt` value is in the past.

For every batch it:

1. marks links as inactive in PostgreSQL;
2. commits the database transaction;
3. removes matching Redis keys;
4. logs the number of processed links.

A failed cleanup iteration is logged and retried during the next interval.

## Security boundaries

- Passwords are stored only as ASP.NET Core Identity password hashes.
- JWT signing keys are supplied through User Secrets or environment variables.
- Link-management and analytics endpoints require authentication.
- Database queries include the authenticated owner identifier.
- Foreign links return `404` instead of exposing their existence.
- Only absolute HTTP and HTTPS target URLs are accepted.