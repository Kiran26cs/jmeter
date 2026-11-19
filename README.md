# ProductApi + JMeter Load Testing

```
ProductApi/                 .NET Web API (in-memory data, http://localhost:5000)
jmeter/ProductApi.jmx       Parameterised JMeter plan (-Jthreads, -Jrampup, -Jduration, -Jhost, -Jport)
jmeter/ProductAPIManual.jmx Plan built by hand in the JMeter GUI (fixed values, edit in GUI)
results/                    Test output (git-ignored: .jtl files can be hundreds of MB)
```

## Endpoints
| Method | Path | Purpose |
|---|---|---|
| GET | `/health` | liveness |
| GET | `/api/products?limit=20&after=<cursor>` | cursor-paged list (default 20, max 100); returns `{ items, nextCursor }`, `nextCursor` is null on the last page |
| GET | `/api/products/{id}` | read one |
| POST | `/api/products` | create (201) |
| DELETE | `/api/products/{id}` | delete |
| GET | `/api/products/slow` | 200-800 ms simulated downstream latency |

Why cursor paging: an earlier version returned the whole list, so response size and latency grew with every POST during a load test (13 KB -> 453 KB per response). Paging keeps responses small and flat.

## 1. Prerequisites
- .NET SDK (the project targets the SDK it was created with)
- JDK 8+ (`java -version`)
- Apache JMeter 5.6.x (https://jmeter.apache.org/download_jmeter.cgi) on PATH

## 2. Run the API (Release, never Debug, for perf tests)
```powershell
cd ProductApi
dotnet run -c Release          # http://localhost:5000
```
Restart the API between runs so each starts with the same 100 seeded products.

## 3. Build/inspect a plan in the GUI (GUI is for design and debugging only)
```powershell
jmeter -t jmeter\ProductApi.jmx
```
Add a View Results Tree temporarily, run 1 thread / 1 loop and confirm every request is green. Disable listeners before real runs.

## 4. Run real tests in non-GUI mode
```powershell
mkdir results
jmeter -n -t jmeter\ProductApi.jmx -Jthreads=50 -Jrampup=60 -Jduration=300 `
       -l results\run1.jtl -e -o results\run1-report
```
Open `results\run1-report\index.html`. Use a new `.jtl` and report folder for every run (`-o` must not exist or be empty; an existing `.jtl` is appended to).

`ProductAPIManual.jmx` has fixed Thread Group values, so `-J` flags do not change it; edit the Thread Group in the GUI.

## 5. Recommended test progression
| Stage | Users | Ramp | Duration | Goal |
|---|---|---|---|---|
| Smoke | 1-5 | 5 s | 60 s | script correctness, baseline latency |
| Load | expected peak (e.g. 50) | 60 s | 5-10 min | meet SLAs under normal peak |
| Stress | step up 100 -> 200 -> 400 ... | 60 s per step | 5 min per step | find the breaking point |
| Spike | 0 -> 300 quickly | 5 s | 5 min | recovery behaviour |
| Soak | 60% of peak | 120 s | 1-4 h | memory leaks, GC, resource exhaustion |

Change one thing at a time between runs so results stay comparable.

## 6. What to check in every report
- **Active Threads Over Time**: confirms the load you intended (ramp, then plateau).
- **Error %** and the Errors table: any rise as users grow means saturation or a bug.
- **p95 / p99 per request**, not the average. Define targets before running (e.g. p95 < 300 ms, errors < 1%).
- **Throughput vs users**: should rise with users; a plateau with rising latency is the capacity limit.
- **Response Times Over Time** and **Bytes Throughput**: trends, such as drift that suggests growth or leaks.
- Server side (JMeter cannot see it): CPU, memory, GC, thread-pool queue (`dotnet-counters monitor --process-id <pid>`).

Concurrent users are not the same as simultaneous requests: `users ~= throughput x (response time + think time)`. The 500 ms Constant Timer is think time; it caps throughput (about 2 requests/s per user per request type).

## 7. Good practices
- Run JMeter on a separate machine from the API for real numbers; on one machine they compete for CPU, and thousands of connections can exhaust Windows ephemeral ports (`java.net.BindException: Address already in use`), which is a load-generator limit, not an API fault.
- Set JMeter heap for large runs: `$env:HEAP="-Xms2g -Xmx4g"`.
- Do not save response bodies or use heavy listeners in non-GUI runs.
- This sample keeps data in memory, so POSTs grow memory during long runs.

## 8. Adapting this to your own API
1. Copy a plan and change HTTP Request Defaults and the samplers.
2. Authentication: login sampler + JSON Extractor for the token, then a Header Manager with `Authorization: Bearer ${token}`.
3. Data-driven input: CSV Data Set Config.
4. Follow `nextCursor` with a JSON Extractor to test real paging.
5. Run in CI with `jmeter -n ...` and fail on thresholds read from the `.jtl`.
