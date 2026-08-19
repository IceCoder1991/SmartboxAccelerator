# Releases and client upgrades

Platform releases use SemVer (`MAJOR.MINOR.PATCH`) in `release.json`, image tags, Git tags (`vX.Y.Z`), and deployment/client labels. Supported fixes are prepared on `release/X.Y` branches and merged forward; clients never receive branches. Major versions may break overlay/schema compatibility, minor versions remain database-compatible with the prior minor during rollout, and patches are backward-compatible.

Before upgrading, update a candidate overlay's `platformVersion`, then run `smartboxx check-compatibility`. Resolve every reported configuration schema, prompt, document schema, integration/extension, and deployment-overlay issue; execute evaluation and rendered-manifest checks. Back up persistent data, run expand-only migrations, deploy canaries, migrate data, and contract only after the rollback window.

Rollback uses the prior Git tag, images, client configuration, prompts, schemas, extensions, and rendered overlay. Database rollback is forward repair: never downgrade after an irreversible migration. `release.json` records the minimum database compatibility contract. Security fixes may explicitly shorten the support window in release notes.
