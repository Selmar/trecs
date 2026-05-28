# TrecsDictionary

!!! warning "Experimental"
    `TrecsDictionary` is part of the experimental dynamic-collection family — API and backing-storage details may change in future 0.x releases.

`TrecsDictionary<TKey, TValue>` is a growable hash dictionary whose backing storage lives on the world's native heap. Like the other [dynamic collections](dynamic-collections.md), it serializes automatically with snapshots and recordings — no custom serializer needed.

The struct stored on a component is a lightweight 4-byte handle. Keys must implement `IEquatable<TKey>`, and both `TKey` and `TValue` must be `unmanaged`.

```csharp
public partial struct CInventory : IEntityComponent
{
    public TrecsDictionary<int, int> Value;  // item ID -> count
}
```

## When to use TrecsDictionary

| Situation | Recommendation |
|---|---|
| Key-value data that lives on an entity component and must survive snapshots/recordings | `TrecsDictionary<TKey, TValue>` |
| Scratch dictionary for a system that is rebuilt every frame (not stored on an entity) | `NativeIterableDictionary<TKey, TValue>` — allocate with `Allocator.TempJob` or `Persistent` and dispose yourself |
| Small bounded set of keys known at compile time | Consider separate component fields or a `FixedArray` with enum indexing |
| Need to share the collection with non-Trecs code | Wrap a `NativeHashMap` in a `NativeUniquePtr` — see [Storing native collections](pointers.md#storing-native-collections) |

`NativeIterableDictionary` is a standalone native collection with the same hash-table algorithm and deterministic iteration order as `TrecsDictionary`, but it owns its own memory via a Unity allocator. It does not integrate with the Trecs heap, so it cannot be stored on a component and will not be included in snapshots. Use it for transient, per-frame or per-system data.

## Allocating

```csharp
var dict = TrecsDictionary.Alloc<int, int>(World, initialCapacity: 8);

World.AddEntity<MyTag>()
    .Set(new CInventory { Value = dict });
```

`initialCapacity` is optional (defaults to 0). Passing a non-zero value pre-allocates the internal bucket and entry arrays, avoiding early re-allocations if you know the approximate size.

## Read and write wrappers

Access to the dictionary goes through `Read(...)` and `Write(...)` methods that return typed wrappers. This lets Unity's job-safety system track read/write conflicts.

```csharp
// Reading
var read = dict.Read(World);
int count = read.Count;
if (read.TryGetValue(itemId, out int qty)) { /* ... */ }
int val = read[itemId]; // throws if key is missing

// Writing
var write = dict.Write(World);
write.Add(itemId, 5);
write[itemId] = 10;       // add-or-update
write.Remove(itemId);
write.Clear();
```

!!! note
    `Write` is a `ref this` extension method. The call site needs a writable reference to the `TrecsDictionary` handle — the same rule as the [`NativeUniquePtr` gotcha](../guides/gotchas.md#mutating-a-nativeuniqueptrt-needs-write-access-to-the-owning-component). In practice, this means you access the component with `.Write` (not `.Read`) even if you only need to mutate the dictionary contents, because obtaining the `TrecsDictionaryWrite` wrapper requires a `ref` to the handle struct.

## Managed vs native wrappers

`TrecsDictionary` has two sets of wrappers, chosen automatically based on how you resolve the handle:

| Resolved via | Wrapper types | Auto-grows? | Burst-safe? |
|---|---|---|---|
| `WorldAccessor` (main thread) | `TrecsDictionaryRead<K,V>` / `TrecsDictionaryWrite<K,V>` | Yes | No |
| `NativeWorldAccessor` (job) | `NativeTrecsDictionaryRead<K,V>` / `NativeTrecsDictionaryWrite<K,V>` | No | Yes |

Main-thread wrappers can grow the internal storage transparently on `Add` / `GetOrAdd` / indexer-set. Native (Burst) wrappers **cannot** grow — they assert at runtime if capacity is exceeded. Pre-size on the main thread before scheduling:

```csharp
// System.Execute() — main thread
ref var inv = ref handle.Component<CInventory>(World).Write;
inv.Value.EnsureCapacity(World, maxExpectedEntries);
```

## API reference

### Read operations

Available on all four wrapper types.

| Method | Description |
|---|---|
| `Count` | Number of entries. |
| `ContainsKey(key)` | Returns `true` if the key exists. |
| `TryGetValue(key, out value)` | Returns `true` and sets `value` if found. |
| `this[key]` (getter) | Returns the value; throws if key is missing. |
| `TryGetIndex(key, out index)` | Returns `true` and sets the internal dense-array index. |
| `GetKeyAtIndex(index)` | Key at the given dense-array index (debug-asserted bounds). |
| `GetValueAtIndex(index)` | Value at the given dense-array index (`ref readonly` on read wrappers, `ref` on write wrappers). |
| `Keys` | Enumerable over all keys. |
| `GetEnumerator()` | Key-value pair enumeration (see [Iteration](#iteration)). |

### Write operations

Available on `TrecsDictionaryWrite` and `NativeTrecsDictionaryWrite`.

| Method | Description |
|---|---|
| `Add(key, value)` | Insert a new entry. Debug-asserts if the key already exists. |
| `TryAdd(key, value)` | Insert if absent; returns `false` (no-op) if the key already exists. |
| `Set(key, value)` | Update an existing entry. Throws if the key is missing. |
| `this[key] = value` (setter) | Add-or-update: inserts if absent, overwrites if present. |
| `GetOrAdd(key)` | Returns `ref TValue` — inserts a `default` entry if absent. Useful for accumulation patterns. |
| `GetValueByRef(key)` | Returns `ref TValue` for in-place mutation. Throws if key is missing. |
| `Remove(key)` | Remove by key. Returns `false` if not found. Uses swap-back internally — iteration order of remaining entries may change. |
| `Remove(key, out removedValue)` | Same as above, but also returns the removed value. |
| `Clear()` | Remove all entries. |
| `EnsureCapacity(minCapacity)` | Available on `TrecsDictionaryWrite` only (not the native variant — grow on the main thread). |

### Semantic differences between Add, TryAdd, Set, and the indexer

```csharp
var write = dict.Write(World);

// Add — insert only; debug-assert on duplicate
write.Add(42, 100);

// TryAdd — insert only; silent no-op on duplicate
bool added = write.TryAdd(42, 200);  // added == false, value stays 100

// Set — update only; throws if key missing
write.Set(42, 300);                  // value is now 300

// Indexer — add-or-update (most permissive)
write[99] = 500;                     // inserts key 99
write[99] = 600;                     // updates to 600
```

## Iteration

All four wrapper types support `foreach` over key-value pairs. The `Current` property exposes `Key` and `Value`; the pair also supports deconstruction.

```csharp
var read = dict.Read(World);
foreach (var (key, value) in read)
{
    // key : TKey, value : TValue
}
```

To iterate over keys only:

```csharp
foreach (var key in read.Keys)
{
    // ...
}
```

!!! warning
    Do not add or remove entries while iterating. The wrappers track an internal version counter and will assert if the dictionary is mutated through another path while a wrapper is live.

### Index-based access

For advanced patterns — e.g., correlating a dictionary entry with data in a parallel array — use the dense-array index API:

```csharp
var write = dict.Write(World);
if (write.TryGetIndex(key, out int idx))
{
    ref var val = ref write.GetValueAtIndex(idx);
    val.Score += 10;
}
```

## EnsureCapacity and Burst jobs

Main-thread `Add` / `GetOrAdd` / indexer-set auto-grow when the dictionary runs out of capacity (doubling strategy). In a Burst job, the native wrappers **cannot** allocate — they assert on overflow.

The standard pattern is to call `EnsureCapacity` on the main thread before scheduling:

```csharp
public partial class InventoryUpdateSystem : ISystem
{
    WorldAccessor World { get; set; }

    public void Execute(EntityHandle handle)
    {
        // Pre-size on main thread
        ref var inv = ref handle.Component<CInventory>(World).Write;
        inv.Value.EnsureCapacity(World, estimatedMaxEntries);
    }

    [ForEachEntity(typeof(InventoryTag))]
    [WrapAsJob]
    static void ProcessItem(ref CInventory inv, in NativeWorldAccessor world)
    {
        var write = inv.Value.Write(world);
        write.TryAdd(itemId, 1);   // safe — capacity was pre-sized
    }
}
```

`EnsureCapacity` is a no-op if the current capacity already meets the requested minimum.

## Disposing

`TrecsDictionary` must be manually disposed when the owning entity is removed — Trecs does not auto-dispose heap allocations. Use an `OnRemoved` observer:

```csharp
[ForEachEntity]
void OnEntityRemoved(in CInventory inv)
{
    inv.Value.Dispose(World);
}
```

Forgetting to dispose leaks the backing storage. Trecs reports leaks at world shutdown in debug/editor builds.

See [Dynamic Collections — Disposing](dynamic-collections.md#disposing) and [Pointers — cleanup is manual](pointers.md#cleanup-is-manual-for-entity-owned-pointers) for the full observer pattern.

## See also

- [Dynamic Collections](dynamic-collections.md) — overview of `TrecsList`, `TrecsArray`, and `TrecsDictionary` with comparison table.
- [Jobs & Burst](../performance/jobs-and-burst.md) — general Burst/job patterns.
- [Gotchas — NativeUniquePtr write access](../guides/gotchas.md#mutating-a-nativeuniqueptrt-needs-write-access-to-the-owning-component) — same `ref this` rule applies to `TrecsDictionary.Write`.
