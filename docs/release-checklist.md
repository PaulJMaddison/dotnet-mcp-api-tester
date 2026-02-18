# Release checklist

## Pre-release

- [ ] `main` branch is green (build + tests).
- [ ] Version/changelog updated for release.
- [ ] `appsettings` templates reviewed (no secrets committed).
- [ ] Production keys and connection strings present in secret manager.
- [ ] `db/schema.sql` regenerated if migrations changed.

## Deployment readiness

- [ ] Container image built and tagged.
- [ ] Deployment manifest points at correct image tag.
- [ ] `ASPNETCORE_ENVIRONMENT=Production` set.
- [ ] TLS certificates and ingress/reverse proxy validated.
- [ ] Health endpoint (`/health`) responds after rollout.

## Database

- [ ] Backup/snapshot taken before migration.
- [ ] Idempotent migration script applied (`db/schema.sql`).
- [ ] Migration history table validated.
- [ ] App smoke tests pass against migrated database.

## Security and observability

- [ ] API keys rotated or verified for release.
- [ ] Logs verified to avoid keys and PII.
- [ ] Alerting and dashboards validated.
- [ ] Incident contact + rollback owner confirmed.

## Post-release

- [ ] Smoke tests pass (create project, run test, list runs).
- [ ] Error rate/latency within expected threshold.
- [ ] Billing/subscription checks validated in production.
- [ ] Release notes shared with stakeholders.
