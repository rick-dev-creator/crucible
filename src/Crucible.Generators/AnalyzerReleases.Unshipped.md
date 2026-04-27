; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
CRC001 | Crucible | Error | Aggregate has no entry step
CRC002 | Crucible | Error | Aggregate has multiple entry steps
CRC003 | Crucible | Error | Duplicate step order
CRC004 | Crucible | Error | Step order has a gap
CRC005 | Crucible | Error | Aggregate is not partial
CRC006 | Crucible | Error | Aggregate must derive from AggregateRoot<TId>
CRC007 | Crucible | Error | Step method has invalid return type
CRC008 | Crucible | Error | Step method is async or returns Task
CRC010 | Crucible | Error | Ambiguous handler
CRC100 | Crucible | Info | Step has no handler — runs as domain-only
CRC200 | Crucible | Warning | [Pre<T>] / [Post<T>] target does not implement the expected interface
