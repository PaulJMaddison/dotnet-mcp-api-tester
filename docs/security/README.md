# Security documentation index

- [Threat model](./threat-model.md)
- [Disclosure policy](../../SECURITY.md)
- [Run security regression suite](../../scripts/security.sh) (or `pwsh ./scripts/security.ps1`)

## Running security tests

```bash
./scripts/security.sh
```

```powershell
pwsh ./scripts/security.ps1
```

Results are written to `artifacts/security/<timestamp>/security-results.txt`.
