# Docker Compose Configurations

This project provides two Docker Compose configurations for different use cases.

## Configuration Files

### `docker-compose.yml` (Production/GitHub Images)
Uses pre-built images from GitHub Container Registry.

**Ports:**
- Client: `http://localhost:81`
- PostgreSQL: `localhost:5433`

**Container Names:**
- `rssvibe-docker-postgres`
- `rssvibe-docker-migration`
- `rssvibe-docker-api`
- `rssvibe-docker-client`

**Volume Name:**
- `rssvibe-docker-postgres-data`

**Usage:**
```bash
# Start services
docker compose up -d

# Stop services
docker compose down

# View logs
docker compose logs -f
```

**Access:**
- Client: http://localhost:81
- API (proxied): http://localhost:81/api
- API Documentation: http://localhost:81/api/scalar/v1

---

### `docker-compose.local.yml` (Local Development)
Builds images locally from source code.

**Ports:**
- Client: `http://localhost:82`
- PostgreSQL: `localhost:5434`

**Container Names:**
- `rssvibe-local-postgres`
- `rssvibe-local-migration`
- `rssvibe-local-api`
- `rssvibe-local-client`

**Volume Name:**
- `rssvibe-local-postgres-data`

**Usage:**
```bash
# Start services (builds from source)
docker compose -f docker-compose.local.yml up -d --build

# Stop services
docker compose -f docker-compose.local.yml down

# View logs
docker compose -f docker-compose.local.yml logs -f

# Rebuild after code changes
docker compose -f docker-compose.local.yml up -d --build api
```

**Access:**
- Client: http://localhost:82
- API (proxied): http://localhost:82/api
- API Documentation: http://localhost:82/api/scalar/v1

---

## Test Credentials

Both configurations create a test user automatically:

- **Email:** `test@test.com`
- **Password:** `P@ssw0rd1234`

---

## Port Configuration Summary

| Service    | Aspire (Default) | Production (base) | Local Development |
|------------|------------------|-------------------|-------------------|
| Client     | :80              | :81               | :82               |
| API        | :8080            | :81 (proxy `/api`) | :82 (proxy `/api`) |
| PostgreSQL | :5432            | :5433             | :5434             |

**Note:** All three configurations use different ports so you can run Aspire, production, and local Docker Compose simultaneously without conflicts!

---

## Common Commands

### Clean Up Everything
```bash
# Stop and remove containers, networks, volumes
docker compose down -v
# or for local variant
docker compose -f docker-compose.local.yml down -v
```

### View Service Status
```bash
docker compose ps
# or
docker compose -f docker-compose.local.yml ps
```

### Access Container Shell
```bash
docker exec -it rssvibe-docker-api sh
# or for local
docker exec -it rssvibe-local-api sh
```

### View Specific Service Logs
```bash
docker compose logs -f api
# or
docker compose -f docker-compose.local.yml logs -f api
```

---

## Troubleshooting

### Browser Not Saving Cookies
1. Clear all browser cookies for `localhost`
2. Hard refresh (Ctrl+Shift+R or Cmd+Shift+R)
3. Try in a regular (non-private) browser window

### Services Not Starting
```bash
# Check logs for errors
docker compose logs

# Ensure no port conflicts
lsof -i :8080 -i :80 -i :81 -i :82 -i :5432 -i :5433 -i :5434

# Clean up and restart
docker compose down -v
docker compose up -d
```

### Migration Fails
```bash
# Check PostgreSQL is healthy
docker compose ps postgres

# View migration logs
docker compose logs migration

# Manually trigger migration
docker compose up migration
```
