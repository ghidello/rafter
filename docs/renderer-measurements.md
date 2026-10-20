# Renderer measurement record

Recorded 2026-10-20 20:37:59 (UTC), Microsoft Windows NT 10.0.26220.0, SDK 10.0.402.
See [methodology and limitations](renderer-evaluation.md#cost-comparison). These are measurements, not budgets.
Every process performs a first render; sample 0 is the first launch after the recorded build. Later launches reuse OS/SDK caches, not the renderer instance. First-launch results are visibly noisy. The file-app host includes SDK launch overhead.

## Process and first-render summary

| Variant | Host | First launch ms | Later median ms | Later min–max ms | First render ms (sample 0) | First managed bytes (sample 0) |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| ansi | file-app | 583.625 | 582.380 | 572.219–590.399 | 7.282 | 4160 |
| ansi | project | 100.553 | 100.138 | 98.457–102.852 | 6.229 | 4160 |
| framework | file-app | 577.235 | 572.708 | 561.036–576.786 | 0.165 | 136 |
| framework | project | 94.844 | 96.211 | 93.864–98.515 | 0.163 | 136 |
| full | file-app | 1277.734 | 602.259 | 589.274–612.955 | 24.096 | 129768 |
| full | project | 692.831 | 115.440 | 112.673–118.768 | 25.025 | 129408 |

## Reused writer: 100 warm-up writes, then 1,000 measured writes

| Variant | Host | Microseconds per write | Managed bytes per write |
| --- | --- | ---: | ---: |
| framework | file-app | 0.013 | 0 |
| framework | project | 0.013 | 0 |
| ansi | file-app | 0.833 | 504 |
| ansi | project | 0.627 | 504 |
| full | file-app | 14.536 | 3464 |
| full | project | 13.385 | 3464 |

## Restore/build overhead, excluded from runtime

Existing caches were used. These are not clean restore or full rebuild benchmarks.

| Variant | Host | Restore ms | Build ms |
| --- | --- | ---: | ---: |
| framework | file-app | 674.434 | 658.900 |
| framework | project | 610.270 | 569.695 |
| ansi | file-app | 677.491 | 664.944 |
| ansi | project | 620.508 | 599.095 |
| full | file-app | 677.497 | 1659.308 |
| full | project | 617.150 | 1657.349 |

## All fresh-process samples

| Variant | Host | Sample | Process ms | First render ms | First managed bytes |
| --- | --- | ---: | ---: | ---: | ---: |
| framework | file-app | 0 | 577.235 | 0.165 | 136 |
| framework | project | 0 | 94.844 | 0.163 | 136 |
| ansi | file-app | 0 | 583.625 | 7.282 | 4160 |
| ansi | project | 0 | 100.553 | 6.229 | 4160 |
| full | file-app | 0 | 1277.734 | 24.096 | 129768 |
| full | project | 0 | 692.831 | 25.025 | 129408 |
| framework | file-app | 1 | 572.453 | 0.176 | 136 |
| framework | project | 1 | 95.554 | 0.172 | 136 |
| ansi | file-app | 1 | 582.191 | 6.486 | 4160 |
| ansi | project | 1 | 101.037 | 6.322 | 4160 |
| full | file-app | 1 | 603.792 | 24.323 | 129768 |
| full | project | 1 | 118.768 | 22.572 | 129408 |
| framework | file-app | 2 | 572.708 | 0.166 | 136 |
| framework | project | 2 | 94.322 | 0.163 | 136 |
| ansi | file-app | 2 | 582.380 | 8.234 | 4160 |
| ansi | project | 2 | 101.583 | 6.518 | 4160 |
| full | file-app | 2 | 592.077 | 24.169 | 129768 |
| full | project | 2 | 117.620 | 22.289 | 129408 |
| framework | file-app | 3 | 576.786 | 0.164 | 136 |
| framework | project | 3 | 98.016 | 0.202 | 136 |
| ansi | file-app | 3 | 587.334 | 6.881 | 4160 |
| ansi | project | 3 | 98.457 | 6.072 | 4160 |
| full | file-app | 3 | 602.259 | 24.882 | 129768 |
| full | project | 3 | 113.971 | 22.858 | 129408 |
| framework | file-app | 4 | 570.542 | 0.167 | 136 |
| framework | project | 4 | 93.864 | 0.166 | 136 |
| ansi | file-app | 4 | 584.618 | 6.979 | 4160 |
| ansi | project | 4 | 102.852 | 7.392 | 4160 |
| full | file-app | 4 | 608.399 | 23.630 | 123576 |
| full | project | 4 | 113.796 | 24.306 | 129408 |
| framework | file-app | 5 | 575.069 | 0.160 | 136 |
| framework | project | 5 | 97.410 | 0.163 | 136 |
| ansi | file-app | 5 | 590.399 | 6.642 | 4160 |
| ansi | project | 5 | 99.705 | 6.256 | 4160 |
| full | file-app | 5 | 593.708 | 23.378 | 129768 |
| full | project | 5 | 112.673 | 22.445 | 129408 |
| framework | file-app | 6 | 576.119 | 0.167 | 136 |
| framework | project | 6 | 94.617 | 0.162 | 136 |
| ansi | file-app | 6 | 588.755 | 6.752 | 4160 |
| ansi | project | 6 | 98.776 | 6.277 | 4160 |
| full | file-app | 6 | 597.464 | 23.724 | 129768 |
| full | project | 6 | 117.757 | 23.113 | 129408 |
| framework | file-app | 7 | 567.239 | 0.165 | 136 |
| framework | project | 7 | 98.515 | 0.169 | 136 |
| ansi | file-app | 7 | 572.219 | 6.584 | 4160 |
| ansi | project | 7 | 98.724 | 6.501 | 4160 |
| full | file-app | 7 | 589.274 | 23.967 | 129768 |
| full | project | 7 | 115.165 | 22.709 | 129408 |
| framework | file-app | 8 | 561.036 | 0.159 | 136 |
| framework | project | 8 | 97.959 | 0.170 | 136 |
| ansi | file-app | 8 | 574.721 | 6.887 | 4160 |
| ansi | project | 8 | 100.138 | 6.405 | 4160 |
| full | file-app | 8 | 612.955 | 23.698 | 129768 |
| full | project | 8 | 115.440 | 22.807 | 129408 |
| framework | file-app | 9 | 575.360 | 0.271 | 136 |
| framework | project | 9 | 96.211 | 0.163 | 136 |
| ansi | file-app | 9 | 580.842 | 6.484 | 4160 |
| ansi | project | 9 | 100.912 | 6.368 | 4160 |
| full | file-app | 9 | 608.300 | 23.438 | 129768 |
| full | project | 9 | 115.773 | 22.807 | 129408 |

## Measured source hashes

SHA-256 for the measurement driver and renderer/benchmark code at capture time:

| Source | SHA-256 |
| --- | --- |
| tools/Measure-Renderer.ps1 | 6814e6415104d6ce25a327f2f1a816943a6d2d7f2540fdfa058a684d343f2886 |
| fixtures/renderer/shared/RendererDestination.cs | ec1572839cc57767b88bd71bfafe8982cd7f763fb2bc9cb36705c3ddad38e206 |
| fixtures/renderer/shared/BenchmarkProbe.cs | 2d86b5d0a3fd908671a8630041c4e7086360a1f003e4b18c71388ffb380982fe |
| fixtures/renderer/shared/RendererProbe.cs | 5c610078bb4e9efd2d412aafc98d38d4c878348ee38a55ad7d832d609f52408a |
| fixtures/renderer/ansi/AnsiProbe.cs | aa99975b5a2afedad7ee059e44a37b72fbef663e01fd8fd01d3df19983739bb4 |
| fixtures/renderer/framework/FrameworkProbe.cs | f39178c027cebf979db51de34d4a27950e3bfe759c2ffb97ec463d07ebe8092b |
