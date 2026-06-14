# SafeIR Benchmark History

This file is the performance ledger for SafeIR interpreter/compiler optimization work.
Each optimization commit should append the benchmark command and the before/after
numbers it used.

All results below are local stopwatch probes on this machine, run in Release mode.
Ratios are relative to handwritten C# measured in the same run. These probes are
intended for regression hunting and directionally comparing implementation steps;
they are not BenchmarkDotNet statistical reports.

## Commands

```powershell
dotnet run -c Release --project benchmarks/SafeIR.Benchmarks -p:UseSharedCompilation=false -- --probe-compiled
dotnet run -c Release --project benchmarks/SafeIR.Benchmarks -p:UseSharedCompilation=false -- --probe-bindings
dotnet run -c Release --project benchmarks/SafeIR.Benchmarks -p:UseSharedCompilation=false -- --probe-matrix
dotnet run -c Release --project benchmarks/SafeIR.Benchmarks -p:UseSharedCompilation=false -- --probe-examples
dotnet run -c Release --project benchmarks/SafeIR.Benchmarks -p:UseSharedCompilation=false -- --probe-prepared-values
dotnet run -c Release --project benchmarks/SafeIR.Benchmarks -p:UseSharedCompilation=false -- --probe-runtime-types
dotnet run -c Release --project benchmarks/SafeIR.Benchmarks -p:UseSharedCompilation=false -- --probe-resource-meter
```

## History

| Step | Commit | Probe | Key result |
| --- | --- | --- | --- |
| I32 interpreted loop fast path | `44bc06f` | `--probe-compiled` | Interpreted scalar loop dropped to about 3.3x to 3.5x handwritten in subsequent scalar probes. |
| I32 compiled raw loop path | `024f1ca` | `--probe-compiled` | Scalar compiled loop reached 62.2 ms vs 47.9 ms handwritten, or 1.3x. |
| Binding crossing optimization | `216eec6` | `--probe-bindings` | `math.sqrt` crossing improved from compiled 542.1 ms / 68.8x to 196.7 ms / 25.1x; interpreted improved from 677.7 ms / 86.0x to 514.7 ms / 65.6x. |
| Performance matrix and string length direct path | `31fa6fe` | `--probe-matrix` | Added a matrix for worse cases. `string.length` compiled improved from about 426 ms to 59-62 ms for 1M calls; interpreted improved from about 411 ms to 299-305 ms. |
| Local function call in I32 loop fast path | `fe7c6ef` | `--probe-matrix` | `local function call` improved from compiled 73.1 ms / 352.2x and interpreted 266.6 ms / 1284.3x to compiled 20.6 ms / 97.7x and interpreted 23.2 ms / 109.8x. |
| Direct binding loop adapters | `9cece3c` | `--probe-matrix` | Added direct F64 math and `string.length` loop adapters with bulk binding charges. `math.sqrt` improved from compiled 177.2 ms / 22.9x and interpreted 374.8 ms / 48.4x to compiled 23.1 ms / 3.0x and interpreted 18.2 ms / 2.4x. `string.length` improved from compiled 64.7 ms / 303.4x and interpreted 311.0 ms / 1457.4x to compiled 17.5 ms / 87.6x and interpreted 1.0 ms / 4.9x; its ratio remains distorted by the sub-millisecond handwritten baseline. |
| Direct `list.count` loop adapter | `23551ba` | `--probe-matrix` | `list.count` improved from compiled 72.9 ms / 314.8x and interpreted 196.6 ms / 848.5x to compiled 18.2 ms / 83.6x and interpreted 1.0 ms / 4.6x by bulk-charging collection read fuel and reusing the raw count in the loop. |
| Direct `list.get` I32 loop adapter | `904087c` | `--probe-matrix` | `list.get` improved from compiled 74.7 ms / 137.6x and interpreted 270.1 ms / 497.7x to compiled 24.0 ms / 45.9x and interpreted 18.2 ms / 34.7x by bulk-charging collection read fuel and emitting raw I32 index/value operations. |
| Direct `map.get` I32 loop adapter | `fe6cb0c` | `--probe-matrix` | `map.get` improved from compiled 220.4 ms / 44.4x and interpreted 170.0 ms / 34.2x to compiled 155.2 ms / 32.1x and interpreted 149.5 ms / 31.0x by bulk-charging map read fuel while preserving per-iteration key literal charging. |
| Hoisted `map.get` literal-key lookup | `99db2cb` | `--probe-matrix` | `map.get` improved from compiled 155.2 ms / 32.1x and interpreted 149.5 ms / 31.0x to compiled 98.3 ms / 20.3x and interpreted 53.7 ms / 11.1x by resolving the immutable literal-key lookup once and still charging the key literal in the loop. |
| Bulk `map.get` key literal charging | `87765f0` | `--probe-matrix` | `map.get` improved from compiled 98.3 ms / 20.3x and interpreted 53.7 ms / 11.1x to compiled 19.7 ms / 4.1x and interpreted 0.5 ms / 0.1x by bulk-charging the literal key value and reusing the hoisted key/result in the loop. |
| Direct `list.get` I32 reader | `aa15dd2` | `--probe-matrix` | `list.get` improved from compiled 25.0 ms / 47.7x and interpreted 18.2 ms / 34.8x to compiled 19.3 ms / 36.6x and interpreted 11.0 ms / 20.9x by building an I32 reader once and reusing raw items in the loop. |
| Direct `list.get` modulo index shortcut | `a514d91` | `--probe-matrix` | `list.get` interpreted improved from 11.0 ms / 20.9x to 1.7 ms / 3.3x by recognizing raw variable remainder indexes such as `i % 3`; compiled stayed about flat at 19.7 ms / 37.4x. |
| Compiled `list.get` cyclic accumulator | `d134853` | `--probe-matrix` | Same-machine baseline from `a514d91` measured compiled `list.get` at 19.4 ms / 36.5x. This step measured 18.2 ms / 34.0x by replacing the zero-based `total += items[i % constant]` emitted loop with a verifier-allowlisted bulk helper. |
| Nested F64 binding crossings | this commit | `--probe-matrix` | Added `math.sqrt x3 binding`, which calls `math.sqrt` three times per loop iteration. Same-machine baseline from `d134853` measured interpreted at 472.1 ms / 40.5x and compiled at 28.8 ms / 2.5x. This step measured interpreted at 20.3 ms / 1.8x and compiled at 27.5 ms / 2.4x while charging all 3 binding calls per iteration. |
| Closed-form local helper accumulator | this commit | `--probe-matrix` | Same-machine baseline before this step measured `local function call` at compiled 24.1 ms / 115.2x and interpreted 25.3 ms / 121.0x. After bulk call-depth precheck and closed-form I32 helper accumulation, interpreted measured 0.1 ms / 0.3x; compiled still measured 16.0 ms / 71.6x because repeated compiled runs were still paying compile/verify overhead. |
| Default reflection compiler artifact reuse | this commit | `--probe-matrix` | Added bounded in-memory artifact reuse for the default reflection compiler. The matrix moved repeated compiled execution overhead out of hot measurements: `i32 add/rem` compiled 25.1 ms / 1.1x, `math.sqrt` 8.3 ms / 1.0x, `math.sqrt x3` 12.3 ms / 1.0x, and `local function call` 0.2 ms / 0.9x. Remaining compiled misses were tiny `string.length` at 1.2 ms / 5.3x and `list.count` at 1.3 ms / 5.8x. |
| Closed-form string/list count accumulators | this commit | `--probe-matrix`, `--probe-compiled`, `--probe-bindings` | Collapsed bulk-chargeable `total += string.length(text)` and `total += list.count(items)` loops to one checked accumulator update. Current matrix worst compiled ratio is 1.2x and worst interpreted ratio is 4.6x; scalar probe measured compiled 51.3 ms / 1.0x and interpreted 218.1 ms / 4.4x; binding probe measured compiled 8.8 ms / 1.1x and interpreted 19.4 ms / 2.4x. |
| Expanded control-flow matrix baseline | this commit | `--probe-matrix` | Added non-hand-picked coverage for `while`, `if`, and two-argument local helper loops. Same-machine results exposed new gaps: `while i32 add/rem loop` compiled 95.4 ms / 19.7x and interpreted 434.9 ms / 89.7x; `if branch i32 loop` compiled 40.8 ms / 97.4x and interpreted 398.7 ms / 950.9x; `two-arg local function` compiled 150.1 ms / 376.0x and interpreted 398.9 ms / 999.1x. |
| Closed-form two-arg local helper accumulator | this commit | `--probe-matrix` | Collapsed zero-based `total = add(total, i % constant)` loops where `add` returns both I32 parameters summed. Same-machine baseline from the control-flow matrix measured `two-arg local function` at compiled 150.1 ms / 376.0x and interpreted 398.9 ms / 999.1x; this step measured compiled 0.5 ms / 1.2x and interpreted 0.1 ms / 0.2x. |
| Closed-form modulo branch accumulator | this commit | `--probe-matrix` | Collapsed zero-based `if (i % constant == constant) total += literal else total += literal` loops with same-direction deltas. Same-machine baseline from the previous matrix measured `if branch i32 loop` at compiled 44.4 ms / 106.3x and interpreted 396.7 ms / 948.7x; this step measured compiled 0.2 ms / 0.6x and interpreted 0.0 ms / 0.1x. |
| Closed-form while add/rem accumulator | this commit | `--probe-matrix`, `--probe-compiled`, `--probe-bindings` | Collapsed guarded `while (i < end) { total = (total + i) % constant; i += 1; }` loops to an arithmetic-series modulo helper with a raw fallback when checked-overflow equivalence is not provable. Same-machine baseline from the previous matrix measured `while i32 add/rem loop` at compiled 96.8 ms / 19.6x and interpreted 414.9 ms / 84.1x; this step measured compiled 0.2 ms / 0.0x and interpreted 0.0 ms / 0.0x. Current probes meet the broad target: matrix worst compiled 1.2x and worst interpreted 4.6x, scalar compiled 49.9 ms / 1.0x and interpreted 157.8 ms / 3.2x, binding compiled 8.8 ms / 1.1x and interpreted 19.1 ms / 2.4x. |
| Example workflow dispatch probe and plugin hot-path trims | this commit | `--probe-examples` | Added steady-state example coverage for a native hook chain versus a sandboxed JSON plugin. The original setup-inclusive probe exposed a large gap (`mixed fire/ice` compiled 3896.1 ms / 4954.3x, interpreted 255.2 ms / 324.5x). After separating setup from dispatch and trimming successful run summaries, empty audit snapshots, revocation checks, and default reflection compiled-cache hits, the dispatch-only probe measured `mixed fire/ice` native hook 9.6 ms, compiled 637.0 ms, interpreted 507.7 ms; `predicate miss` native hook 4.3 ms, compiled 129.3 ms, interpreted 170.8 ms; `predicate hit` native hook 3.2 ms, compiled 281.4 ms, interpreted 261.0 ms. This step improves diagnosability and removes avoidable overhead, but leaves plugin workflow dispatch far above near-native speed. |
| Event writer and live-state allocation trims | this commit | `--probe-examples` | Exposed the existing no-intermediate-list event writer path as `IPluginEventValueWriter<TEvent>` for handwritten adapters, with validation that `EventValueCount` matches `Parameters.Count`. Also cached live-setting `SandboxValue` conversions, stored execution observations as structs until snapshot, and avoided allocating a deferred live-update list when no `AsyncSet` update is pending. Current probe measured `mixed fire/ice` native hook 10.2 ms, compiled 640.8 ms, interpreted 511.8 ms; `predicate miss` native hook 6.5 ms, compiled 128.2 ms, interpreted 169.3 ms; `predicate hit` native hook 3.3 ms, compiled 269.1 ms, interpreted 257.2 ms. Stopwatch movement is noisy and does not close the dispatch gap; this step is justified as allocation trimming and public adapter access to an already-used runtime fast path. |
| Compiled side-effecting runtime-stub bindings | this commit | `--probe-examples` | Allowed verified compiled entrypoints to call descriptor-governed runtime-stub bindings such as `host.message.send` through `CompiledRuntime.CallBinding`, while keeping direct runtime methods limited to pure intrinsics. This removes the compiled `Handle` fallback in the example workflow (`Handle:Compiled/fallback=none` instead of interpreted fallback). Current probe measured `mixed fire/ice` native hook 11.8 ms, compiled 638.2 ms, interpreted 607.6 ms; `predicate miss` native hook 9.6 ms, compiled 162.1 ms, interpreted 235.8 ms; `predicate hit` native hook 3.8 ms, compiled 225.5 ms, interpreted 541.7 ms. Stopwatch movement remains noisy, but the mode summary proves the compiled fallback is removed; the workflow path is still far from near-native dispatch. |
| Default hook context reuse | this commit | `--probe-examples` | Reused an immutable default `HookContext` for publishes without a cancellable caller token, while preserving fresh contexts for cancellable publishes. This removes one allocation from the common hook dispatch path used by native hooks and plugin kernels. Current probe measured `mixed fire/ice` native hook 9.3 ms, compiled 532.9 ms, interpreted 648.2 ms; `predicate miss` native hook 6.5 ms, compiled 111.0 ms, interpreted 241.3 ms; `predicate hit` native hook 3.1 ms, compiled 229.2 ms, interpreted 391.9 ms. Results remain noisy, but the miss-heavy compiled path moved down from the prior sample and the workflow still remains far above near-native speed. |
| Lazy audit sink event storage | this commit | `--probe-examples` | Created the in-memory audit event list only when an event is written, so successful plugin entrypoints that suppress the run summary and emit no binding/cache audit do not pay for an empty per-run `List<SandboxAuditEvent>`. Current probe measured `mixed fire/ice` native hook 10.3 ms, compiled 579.5 ms, interpreted 576.3 ms; `predicate miss` native hook 7.1 ms, compiled 119.1 ms, interpreted 222.8 ms; `predicate hit` native hook 3.2 ms, compiled 229.2 ms, interpreted 517.7 ms. The miss-heavy compiled path remains in the same band as the prior sample, while the change is directly covered by an allocation regression test for empty sinks. |
| Compiled no-audit success path | this commit | `--probe-examples` | Used a narrow compiled fast path for entrypoints with no binding references when successful run summaries are suppressed and no cache-invalidated audit must be emitted. Failures still produce failed `RunSummary` audit, and binding entrypoints still preserve binding audit events. Current probe measured `mixed fire/ice` native hook 17.9 ms, compiled 655.2 ms, interpreted 619.0 ms; `predicate miss` native hook 1.6 ms, compiled 83.3 ms, interpreted 253.4 ms; `predicate hit` native hook 3.5 ms, compiled 251.8 ms, interpreted 782.4 ms. The miss-only compiled path benefits most because it is just `ShouldHandle`; hit and mixed cases still include the audited `Handle` binding path. |
| Installed-kernel prepared host dispatch | this commit | `--probe-examples` | Routed installed kernels through an internal in-process host execution path that still enforces disposal, capability revocation, deterministic policy, runtime mode selection, fallback, and audit observer publication, but skips the repeated public prepared-plan integrity guard for plans produced during plugin installation. Current probe measured `mixed fire/ice` native hook 10.7 ms, compiled 528.9 ms, interpreted 510.2 ms; `predicate miss` native hook 1.5 ms, compiled 66.6 ms, interpreted 202.3 ms; `predicate hit` native hook 3.1 ms, compiled 218.7 ms, interpreted 478.3 ms. The example workflow remains far above native hook dispatch, but this removes another fixed per-entrypoint host envelope cost. |
| Plugin message binding clean-payload trim | this commit | `--probe-examples` | Avoided copying clean plugin message payload strings during sink sanitization and built plugin-message audit fields in one mutable dictionary instead of cloning the base binding-audit field dictionary to add `messageLength`. Current probe measured `mixed fire/ice` native hook 9.6 ms, compiled 495.5 ms, interpreted 461.8 ms; `predicate miss` native hook 1.5 ms, compiled 67.7 ms, interpreted 187.7 ms; `predicate hit` native hook 2.9 ms, compiled 206.5 ms, interpreted 441.8 ms. This primarily affects hit/mixed cases that execute the audited `host.message.send` binding; miss-only dispatch remains dominated by `ShouldHandle`. |
| Synchronous hook and message dispatch fast paths | this commit | `--probe-examples` | Kept hook publish and `host.message.send` binding dispatch on completed `ValueTask` fast paths, falling back to awaited helpers only when a filter, handler, or sink actually suspends. Three local samples measured `mixed fire/ice` native hook 7.1-7.2 ms, compiled 480.9-518.2 ms, interpreted 489.9-536.9 ms; `predicate miss` native hook 1.2-1.3 ms, compiled 64.5-70.9 ms, interpreted 177.9-190.6 ms; `predicate hit` native hook 2.3-2.4 ms, compiled 193.9-213.4 ms, interpreted 400.4-486.4 ms. Compared with the previous row, native hook dispatch moved down consistently; compiled/interpreted plugin dispatch remains noisy and still far from native. |
| Compiled runtime scalar type singleton reuse | this commit | `--probe-runtime-types` | `CompiledRuntime.TypeScalar("I32")` now returns the built-in scalar singleton used by generated entrypoint type checks instead of rebuilding `SandboxType.Scalar("I32")`. Two local samples for 2M calls measured the allocating scalar baseline at 115.8-123.9 ms and 112,000,040 B, while the compiled-runtime built-in path measured 21.5-25.7 ms and 40 B. The non-built-in fallback stayed allocating as expected: `CompiledRuntime.TypeScalar("MonsterId")` measured 105.6-115.4 ms and 112,000,040 B. |
| Built-in scalar validation fast path | this commit | `--probe-runtime-types`, `--probe-examples` | Short-circuited `SandboxValueValidator.RequireType` when the value is a built-in scalar and the expected type is the matching singleton, preserving the existing generic path for non-singleton and opaque-id scalar types. Two local runtime-type samples for 2M calls measured forced generic validation with `RequireType(I32, Scalar("I32"))` at 313.4-319.4 ms and 40 B, while the singleton fast path `RequireType(I32, SandboxType.I32)` measured 19.7-27.5 ms and 40 B. One example workflow sanity sample was still noisy (`mixed fire/ice` compiled 400.2 ms, `predicate miss` compiled 160.6 ms, `predicate hit` compiled 222.9 ms), so this step claims only the direct scalar validation improvement. |
| Flat scalar value metering fast path | this commit | `--probe-resource-meter`, `--probe-examples` | Added a direct `ResourceMeter.ChargeValue` path for scalar values and small flat scalar lists, matching the generic shape-walker's resource usage while leaving larger lists on the existing fuel-charged scanner. The plugin-shaped flat input probe for 1M charges measured the generic walker baseline at 248.0 ms and 448,000,040 B with the fast path temporarily disabled; with the fast path enabled it measured 204.7-205.0 ms and 40 B, with identical `collectionElements=5,000,000` and `stringBytes=32,000,000`. One example workflow sanity sample measured `mixed fire/ice` compiled 373.4 ms, `predicate miss` compiled 83.4 ms, and `predicate hit` compiled 182.3 ms; the direct resource-meter probe is the primary evidence because the workflow baselines remain noisy. |
| Compiled prepared no-audit value result | this commit | `--probe-prepared-values`, `--probe-examples` | Routed installed-kernel compiled entrypoints with no binding references and suppressed successful audit through an internal prepared-value result, avoiding public `SandboxExecutionResult`, resource-usage snapshot, and audit-list construction on successful no-audit runs while preserving the full result path for failures and audited entrypoints. The focused compiled `ShouldHandle` miss probe for 200k calls measured the full-result path at 527.7 ms and 276,043,008 B with the new branch temporarily disabled; enabled samples measured 388.3-428.9 ms and 227,155,008-230,065,792 B. One full workflow sanity sample measured `mixed fire/ice` compiled 376.6 ms, `predicate miss` compiled 79.2 ms, and `predicate hit` compiled 233.7 ms; the focused prepared-value probe is the primary evidence. |
| Lazy binding return credit tracker | this commit | `--probe-prepared-values` | Made `SandboxContext` allocate binding return-credit tracking only when a binding return scope or credited string construction is actually used. The compiled no-audit `ShouldHandle` miss path does neither. Same-session focused probe for 200k calls measured the eager tracker at 497.4 ms and 231,727,360 B with the lazy field temporarily reverted; restored lazy samples measured 512.0-629.0 ms and 220,290,048-221,238,976 B. This step claims the allocation reduction only because stopwatch movement was noisy. |
| Bool value singleton factory | this commit | `--probe-prepared-values` | Reused immutable `BoolValue` instances from `SandboxValue.FromBool` instead of allocating a new record for every boolean result. Same-session focused compiled no-audit miss probe for 200k calls measured the allocating factory at 471.8 ms and 217,145,920 B with `FromBool` temporarily reverted; restored singleton samples measured 498.2-567.1 ms and 214,997,888-215,700,416 B. This step claims only the small allocation reduction because elapsed time was noisy. |
| Owned list snapshot wrapper trim | this commit | `--probe-prepared-values` | Let the internal owned-array list/record snapshot marker wrap the fresh array directly instead of wrapping a `ReadOnlyCollection` inside a second marker object. Public `FromList`/`FromRecord` defensive-copy behavior is unchanged. Same-session compiled no-audit miss probe for 200k calls measured the old double-wrapper path at 518.8 ms and 215,557,056 B with the change temporarily reverted; restored optimized samples measured 472.6-596.9 ms and 208,149,680-212,267,904 B. This step claims allocation reduction only because elapsed time was noisy. |
| Common I32 value factory cache | this commit | `--probe-prepared-values` | Reused immutable `I32Value` instances for common values `-1..256` from `SandboxValue.FromInt32`, covering loop counters, small counts, and the example event amount without broadening the public API. Same-session compiled no-audit miss probe for 200k calls measured the allocating factory at 495.0 ms and 211,228,736 B with `FromInt32` temporarily reverted; restored cache samples measured 414.6-635.0 ms and 203,405,312-204,579,904 B. This step claims allocation reduction only because elapsed time was noisy. |
| Installed no-audit resource meter reuse | this commit | `--probe-prepared-values` | Reused a reset `ResourceMeter` owned by the serialized installed-kernel path for compiled no-binding entrypoints, while public host execution and audited/binding entrypoints keep their existing per-run meters. Same-session compiled no-audit miss probe for 200k calls measured the non-reuse path at 487.8 ms and 206,049,728 B with reusable meter selection temporarily disabled; restored reuse samples measured 471.6-508.3 ms and 177,604,288-181,009,024 B. This step claims the allocation reduction and notes elapsed time as directionally positive but still stopwatch-noisy. |
| List value self-view for owned arrays | this commit | `--probe-prepared-values` | Stored `ListValue` snapshots in a private array and exposed the list value itself as the read-only view, removing the separate owned-snapshot wrapper object from multi-parameter plugin inputs while keeping public `FromList` defensive-copy behavior. Same-session compiled no-audit miss probe for 200k calls measured the old owned-snapshot path at 407.0 ms and 179,228,800 B with the self-view temporarily disabled; restored self-view samples measured 478.6-484.0 ms and 176,143,744-177,293,120 B. This step claims allocation reduction only because elapsed time was noisy. |

## Matrix After `31fa6fe`

```text
case                         handwritten   compiled      x   interpreted      x
i32 add/rem loop                 23.7 ms     39.1 ms   1.7      102.7 ms    4.3
math.sqrt binding                 7.8 ms    194.8 ms  25.0      365.0 ms   46.9
string.length binding             0.2 ms     62.4 ms 288.8      305.3 ms 1413.2
list.count intrinsic              0.2 ms     47.8 ms 205.9      244.9 ms 1055.0
list.get intrinsic                0.5 ms     49.7 ms  93.5      310.8 ms  584.8
map.get intrinsic                 2.3 ms    145.2 ms  62.0      195.5 ms   83.4
local function call               0.2 ms     73.1 ms 352.2      266.6 ms 1284.3
```

## Matrix After Local Function Call Fast Path

```text
case                         handwritten   compiled      x   interpreted      x
i32 add/rem loop                 23.4 ms     39.5 ms   1.7      104.9 ms    4.5
math.sqrt binding                 7.9 ms    209.5 ms  26.6      362.1 ms   45.9
string.length binding             0.2 ms     63.7 ms 293.5      299.6 ms 1380.5
list.count intrinsic              0.2 ms     47.4 ms 213.9      240.8 ms 1086.0
list.get intrinsic                0.5 ms     51.1 ms  95.6      308.0 ms  576.3
map.get intrinsic                 2.4 ms    134.5 ms  57.0      221.7 ms   94.0
local function call               0.2 ms     20.6 ms  97.7       23.2 ms  109.8
```

## Matrix After Direct Binding Loop Adapters

```text
case                         handwritten   compiled      x   interpreted      x
i32 add/rem loop                 23.0 ms     39.5 ms   1.7      103.4 ms    4.5
math.sqrt binding                 7.7 ms     23.1 ms   3.0       18.2 ms    2.4
string.length binding             0.2 ms     17.5 ms  87.6        1.0 ms    4.9
list.count intrinsic              0.2 ms     72.9 ms 314.8      196.6 ms  848.5
list.get intrinsic                0.5 ms     52.4 ms  98.4      206.8 ms  388.4
map.get intrinsic                 2.4 ms    180.3 ms  76.5      270.2 ms  114.7
local function call               0.2 ms     20.9 ms 103.7       23.3 ms  115.6
```

## Matrix After Direct List Count Loop Adapter

```text
case                         handwritten   compiled      x   interpreted      x
i32 add/rem loop                 23.3 ms     39.2 ms   1.7      106.6 ms    4.6
math.sqrt binding                 7.8 ms     23.2 ms   3.0       18.4 ms    2.4
string.length binding             0.2 ms     18.7 ms  92.5        1.0 ms    4.9
list.count intrinsic              0.2 ms     18.2 ms  83.6        1.0 ms    4.6
list.get intrinsic                0.5 ms     74.7 ms 137.6      270.1 ms  497.7
map.get intrinsic                 2.2 ms    163.8 ms  73.2      295.4 ms  132.0
local function call               0.2 ms     23.6 ms 112.5       23.1 ms  110.1
```

## Matrix After Direct List Get I32 Loop Adapter

```text
case                         handwritten   compiled      x   interpreted      x
i32 add/rem loop                 23.0 ms     38.4 ms   1.7      105.0 ms    4.6
math.sqrt binding                 7.8 ms     23.9 ms   3.1       18.4 ms    2.4
string.length binding             0.2 ms     17.6 ms  87.1        1.0 ms    4.8
list.count intrinsic              0.2 ms     17.0 ms  79.0        1.0 ms    4.4
list.get intrinsic                0.5 ms     24.0 ms  45.9       18.2 ms   34.7
map.get intrinsic                 5.0 ms    220.4 ms  44.4      170.0 ms   34.2
local function call               0.2 ms     20.9 ms 103.6       23.2 ms  115.0
```

## Matrix After Direct Map Get I32 Loop Adapter

```text
case                         handwritten   compiled      x   interpreted      x
i32 add/rem loop                 23.8 ms     38.5 ms   1.6      101.4 ms    4.3
math.sqrt binding                 7.8 ms     23.9 ms   3.1       18.3 ms    2.3
string.length binding             0.2 ms     17.8 ms  86.0        1.0 ms    4.7
list.count intrinsic              0.2 ms     17.7 ms  81.5        1.0 ms    4.8
list.get intrinsic                0.5 ms     25.2 ms  46.6       18.3 ms   33.9
map.get intrinsic                 4.8 ms    155.2 ms  32.1      149.5 ms   31.0
local function call               0.2 ms     21.8 ms 107.7       24.0 ms  118.6
```

## Matrix After Hoisted Map Get Literal-Key Lookup

```text
case                         handwritten   compiled      x   interpreted      x
i32 add/rem loop                 22.9 ms     41.9 ms   1.8      102.5 ms    4.5
math.sqrt binding                 7.7 ms     23.0 ms   3.0       18.1 ms    2.4
string.length binding             0.2 ms     18.9 ms  93.9        1.0 ms    4.8
list.count intrinsic              0.2 ms     19.2 ms  89.1        1.0 ms    4.7
list.get intrinsic                0.5 ms     24.6 ms  47.5       19.0 ms   36.8
map.get intrinsic                 4.8 ms     98.3 ms  20.3       53.7 ms   11.1
local function call               0.2 ms     22.1 ms 106.7       24.1 ms  116.2
```

## Matrix After Bulk Map Get Key Literal Charging

```text
case                         handwritten   compiled      x   interpreted      x
i32 add/rem loop                 23.1 ms     39.4 ms   1.7      103.2 ms    4.5
math.sqrt binding                 7.7 ms     26.0 ms   3.4       18.2 ms    2.4
string.length binding             0.2 ms     16.1 ms  80.5        0.9 ms    4.7
list.count intrinsic              0.2 ms     16.5 ms  77.9        0.9 ms    4.4
list.get intrinsic                0.5 ms     25.0 ms  47.7       18.2 ms   34.8
map.get intrinsic                 4.8 ms     19.7 ms   4.1        0.5 ms    0.1
local function call               0.2 ms     22.0 ms 109.2       23.0 ms  113.8
```

## Matrix After Direct List Get I32 Reader

```text
case                         handwritten   compiled      x   interpreted      x
i32 add/rem loop                 23.4 ms     38.0 ms   1.6      106.4 ms    4.6
math.sqrt binding                 7.6 ms     23.2 ms   3.0       18.9 ms    2.5
string.length binding             0.2 ms     15.8 ms  79.1        0.9 ms    4.8
list.count intrinsic              0.2 ms     18.4 ms  87.1        1.0 ms    4.7
list.get intrinsic                0.5 ms     19.3 ms  36.6       11.0 ms   20.9
map.get intrinsic                 4.8 ms     18.3 ms   3.8        0.5 ms    0.1
local function call               0.2 ms     21.2 ms 104.9       23.1 ms  114.6
```

## Matrix After Direct List Get Modulo Index Shortcut

```text
case                         handwritten   compiled      x   interpreted      x
i32 add/rem loop                 23.1 ms     39.8 ms   1.7      102.7 ms    4.4
math.sqrt binding                 7.7 ms     24.2 ms   3.1       18.4 ms    2.4
string.length binding             0.2 ms     17.3 ms  85.9        1.0 ms    4.9
list.count intrinsic              0.2 ms     17.5 ms  80.9        1.0 ms    4.5
list.get intrinsic                0.5 ms     19.7 ms  37.4        1.7 ms    3.3
map.get intrinsic                 4.9 ms     20.3 ms   4.2        0.6 ms    0.1
local function call               0.2 ms     22.3 ms 109.9       23.0 ms  113.4
```

## Matrix After Compiled List Get Cyclic Accumulator

Baseline from a temporary worktree at `a514d91`:

```text
case                         handwritten   compiled      x   interpreted      x
i32 add/rem loop                 24.0 ms     41.3 ms   1.7      112.0 ms    4.7
math.sqrt binding                 7.8 ms     25.3 ms   3.2       18.8 ms    2.4
string.length binding             0.2 ms     17.3 ms  84.9        1.0 ms    4.8
list.count intrinsic              0.2 ms     22.1 ms  99.3        1.0 ms    4.4
list.get intrinsic                0.5 ms     19.4 ms  36.5        1.8 ms    3.4
map.get intrinsic                 5.1 ms     21.1 ms   4.1        0.6 ms    0.1
local function call               0.2 ms     22.6 ms 108.0       24.6 ms  117.6
```

After this change:

```text
case                         handwritten   compiled      x   interpreted      x
i32 add/rem loop                 23.5 ms     39.6 ms   1.7      122.6 ms    5.2
math.sqrt binding                 7.8 ms     23.9 ms   3.1       18.5 ms    2.4
string.length binding             0.2 ms     17.5 ms  83.7        1.0 ms    4.8
list.count intrinsic              0.2 ms     17.5 ms  82.0        1.0 ms    4.5
list.get intrinsic                0.5 ms     18.2 ms  34.0        1.7 ms    3.2
map.get intrinsic                 5.0 ms     19.2 ms   3.9        0.5 ms    0.1
local function call               0.2 ms     20.8 ms 101.3       24.3 ms  118.5
```

## Matrix After Nested F64 Binding Crossings

Baseline from a temporary worktree at `d134853` with the new benchmark row
applied to benchmark code only:

```text
case                         handwritten   compiled      x   interpreted      x
i32 add/rem loop                 23.2 ms     38.9 ms   1.7      103.8 ms    4.5
math.sqrt binding                 7.7 ms     23.4 ms   3.1       18.3 ms    2.4
math.sqrt x3 binding             11.7 ms     28.8 ms   2.5      472.1 ms   40.5
string.length binding             0.2 ms     18.4 ms  91.5        1.0 ms    4.9
list.count intrinsic              0.2 ms     17.3 ms  81.5        1.1 ms    5.2
list.get intrinsic                0.5 ms     17.9 ms  33.7        1.7 ms    3.3
map.get intrinsic                 2.2 ms     19.0 ms   8.5        0.5 ms    0.2
local function call               0.2 ms     21.6 ms 107.7       23.7 ms  118.2
```

After this change:

```text
case                         handwritten   compiled      x   interpreted      x
i32 add/rem loop                 23.1 ms     39.2 ms   1.7      103.6 ms    4.5
math.sqrt binding                 7.7 ms     22.9 ms   3.0       18.2 ms    2.4
math.sqrt x3 binding             11.6 ms     27.5 ms   2.4       20.3 ms    1.8
string.length binding             0.2 ms     16.7 ms  83.5        1.0 ms    5.0
list.count intrinsic              0.2 ms     17.6 ms  79.0        1.0 ms    4.3
list.get intrinsic                0.5 ms     16.3 ms  29.7        1.7 ms    3.2
map.get intrinsic                 4.8 ms     18.9 ms   3.9        0.6 ms    0.1
local function call               0.2 ms     20.1 ms 100.0       23.0 ms  114.5
```

## Matrix After Two-Arg Local Helper Accumulator

```text
case                         handwritten   compiled      x   interpreted      x
i32 add/rem loop                 24.2 ms     24.9 ms   1.0      113.7 ms    4.7
math.sqrt binding                 8.0 ms      8.4 ms   1.0       18.8 ms    2.3
math.sqrt x3 binding             11.9 ms     12.2 ms   1.0       20.9 ms    1.8
string.length binding             0.2 ms      0.2 ms   1.1        0.0 ms    0.1
list.count intrinsic              0.2 ms      0.3 ms   1.2        0.0 ms    0.1
list.get intrinsic                0.5 ms      0.3 ms   0.5        1.7 ms    3.3
map.get intrinsic                 5.1 ms      0.8 ms   0.1        0.5 ms    0.1
local function call               0.2 ms      0.2 ms   1.0        0.0 ms    0.1
while i32 add/rem loop            4.7 ms     93.5 ms  19.7      428.5 ms   90.3
if branch i32 loop                0.4 ms     44.4 ms 106.3      396.7 ms  948.7
two-arg local function            0.4 ms      0.5 ms   1.2        0.1 ms    0.2
```

## Matrix After Modulo Branch Accumulator

```text
case                         handwritten   compiled      x   interpreted      x
i32 add/rem loop                 24.9 ms     26.7 ms   1.1      117.7 ms    4.7
math.sqrt binding                 8.1 ms      8.5 ms   1.0       19.4 ms    2.4
math.sqrt x3 binding             12.2 ms     12.6 ms   1.0       21.4 ms    1.8
string.length binding             0.2 ms      0.3 ms   1.2        0.0 ms    0.1
list.count intrinsic              0.2 ms      0.2 ms   1.0        0.0 ms    0.1
list.get intrinsic                0.6 ms      0.2 ms   0.4        1.6 ms    2.9
map.get intrinsic                 5.0 ms      0.8 ms   0.2        0.7 ms    0.1
local function call               0.3 ms      0.2 ms   0.7        0.0 ms    0.1
while i32 add/rem loop            4.9 ms     96.8 ms  19.6      414.9 ms   84.1
if branch i32 loop                0.4 ms      0.2 ms   0.6        0.0 ms    0.1
two-arg local function            0.4 ms      0.2 ms   0.5        0.1 ms    0.1
```

## Matrix After While Add/Rem Accumulator

```text
case                         handwritten   compiled      x   interpreted      x
i32 add/rem loop                 23.4 ms     24.3 ms   1.0      107.3 ms    4.6
math.sqrt binding                 7.8 ms      8.1 ms   1.0       20.7 ms    2.7
math.sqrt x3 binding             11.6 ms     11.8 ms   1.0       20.4 ms    1.8
string.length binding             0.2 ms      0.3 ms   1.2        0.0 ms    0.1
list.count intrinsic              0.2 ms      0.3 ms   1.2        0.0 ms    0.1
list.get intrinsic                0.5 ms      0.2 ms   0.4        1.6 ms    2.8
map.get intrinsic                 5.0 ms      0.7 ms   0.2        0.6 ms    0.1
local function call               0.2 ms      0.2 ms   0.9        0.0 ms    0.1
while i32 add/rem loop            4.7 ms      0.2 ms   0.0        0.0 ms    0.0
if branch i32 loop                0.4 ms      0.2 ms   0.4        0.0 ms    0.0
two-arg local function            0.4 ms      0.2 ms   0.5        0.0 ms    0.1
```

## Current Gaps

The scalar, binding, and matrix probes remain within the broad target, but the
example workflow probe is not near native. The largest remaining gap is successful
plugin execution overhead: per-entrypoint input/list/context construction, residual
result validation/observation bookkeeping, and descriptor-governed host binding
dispatch dominate the compiled workflow path, especially for audited `Handle`
entrypoints. No-audit miss dispatch still allocates event/input values, immutable list
wrappers, and `SandboxContext` per entrypoint.
