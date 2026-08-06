# Permission Boundaries

## Read-only

- Repository search／read
- Git status／diff
- Unity inspect
- Test report read
- Public web documentation

## Workspace write

- Task-owned source
- Tests
- Docs
- State／Evidence
- Temporary build output under approved roots

## Explicit human approval

- Merge
- Tag create／move
- Release publication
- Package install／upgrade
- Internal API／Reflection
- Credential use
- Player platform signing
- External upload
- Destructive migration
- Existing release artifact overwrite

## Never

- Force push to protected branch
- Print secrets
- Auto-accept visual quality
- Generic SerializedProperty mutation
- Silent fallback to unsupported API
- Reuse expired approval
- Mark project complete from an assistant message
