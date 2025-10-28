# Entity Framework Core Patterns

---

## Data Access Patterns

- MUST use repository and unit of work patterns for data access abstraction
- MUST use eager loading with `Include()` to prevent N+1 query problems
- MUST apply `AsNoTracking()` for read-only queries to optimize performance
- MUST configure entities using Fluent API (AVOID data annotations)
- SHOULD implement compiled queries for frequently executed operations

---

## Type-Safe JSON Properties (CRITICAL)

**NEVER use string properties for JSON data**

❌ **WRONG**:
```csharp
public class Feed {
    public string Selectors { get; set; }  // Don't do this!
}
```

✅ **CORRECT**:
```csharp
// Create strongly-typed model in Models/ directory
public class FeedSelectors {
    public string Title { get; set; }
    public string Content { get; set; }
}

public class Feed {
    public FeedSelectors Selectors { get; set; }  // Type-safe!
}

// Configure with Fluent API
builder.OwnsOne(x => x.Selectors, b => b.ToJson());
```

**Benefits**: Compile-time type safety, IntelliSense support, better maintainability

---

## Minimal Configuration Approach

**ONLY configure what EF Core cannot infer automatically**

❌ **AVOID** (EF Core infers these automatically):
```csharp
.HasColumnName("id")
.HasColumnType("text")
.IsRequired()  // for non-nullable properties
.ToTable("feed")
```

✅ **DO** (meaningful business rules only):
```csharp
.HasMaxLength(200)
.HasDefaultValue(60)
.HasDefaultValueSql("now()")
.HasCheckConstraint("check_positive", "value > 0")
.ValueGeneratedNever()  // for GUIDs
```

- ONLY specify column types for database-specific features (e.g., `jsonb`, `text[]` for PostgreSQL)

---

## Migration Management

**MUST use the migration script**: `src/RSSVibe.Data/add_migration.sh`

```bash
# Navigate to Data project directory
cd src/RSSVibe.Data

# Create migration (use PascalCase)
bash add_migration.sh AddUserPreferences
```

**Important**:
- MUST execute script from `src/RSSVibe.Data/` directory
- MUST review generated migration file before applying
- NEVER modify migration files manually (remove and regenerate instead)
- Script handles correct project and startup project paths automatically
