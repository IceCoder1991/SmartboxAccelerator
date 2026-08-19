# First-week demo operator guide

Every demo screen, exported result, and spoken walkthrough must retain **DEMO — SYNTHETIC DATA** labelling. Demo fixtures are isolated under a client's `demo/` directory and the fixture declares `productionAllowed: false`; never copy them into production configuration.

- **Day 1 — configure:** Generate and validate the client, replace placeholder branding, and run `seed-demo`. Agree that invoice processing is the synthetic hero workflow and record the value assumptions.
- **Day 2 — rehearse upload:** Show the synthetic invoice upload, progress reaching extraction, and the extracted schema-bound fields. Explain that providers and data are synthetic.
- **Day 3 — validate:** Demonstrate the non-negative-total rule, expected-result comparison, and the queued human-review item.
- **Day 4 — review and value:** Complete review, show the result, dashboard document count, time saved, and estimated ROI. State the assumptions rather than presenting estimates as measured savings.
- **Day 5 — reset and present:** Run `reset-demo`, validate the client, rehearse end-to-end, and retain the visible synthetic banner.

```bash
tools/smartboxx-cli/smartboxx seed-demo clients/client-abc
tools/smartboxx-cli/smartboxx reset-demo clients/client-abc
```

The reset deletes and recreates only deterministic demo fixtures. Activation is valid only in the `demo` environment; deployment automation must reject `demo=true` or `smartboxx.io/synthetic=true` in production.
