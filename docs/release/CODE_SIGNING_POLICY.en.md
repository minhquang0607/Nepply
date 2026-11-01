# Code signing policy

**English** · [Tiếng Việt](CODE_SIGNING_POLICY.md)

## Status

Nepply-owned code is MIT licensed; dependencies keep their own licenses.
An application to SignPath Foundation is being prepared. **It has not been
approved, no certificate has been issued and no production binary is signed.**
QA certificates are never a substitute for release signatures, and users are
never asked to disable Windows protection instead.

## Planned roles

- Author, repository owner and maintainer: Minh Quang
  ([minhquang0607](https://github.com/minhquang0607)).
- Release reviewer and signing approver: Minh Quang, configured explicitly in
  the signing service once approved. Every production signing request is
  approved manually.
- AI team: Codex and Claude help with code and documentation. They do not
  approve releases or signing requests.
- Every participating GitHub/SignPath account uses MFA; tokens are never shared.

## Trust boundaries

- Only Nepply artifacts built from a reviewed commit on a GitHub-hosted runner
  are signed, as the OSS program requires; arbitrary local binaries are not.
- Untrusted pull requests never reach signing secrets. Signing workflows do not
  use `pull_request_target` to run pull request code with secrets.
- Only `Nepply.exe`, `Nepply.DesktopGuardian.exe` and the installer components
  approved by the provider are signed. Third-party libraries are never re-signed
  under the project's name.
- Actions stay pinned to commit SHAs with least-privilege permissions, branch
  protection and a protected release environment.
- Every release keeps a trace: commit → workflow run → artifact → signing request → checksum.

## Pre-release checks

Authenticode validity, the signer matching policy and a timestamp are verified
for the app, guardian, Setup and the actual uninstaller. Manifests and checksums
are produced after signing. Signing Setup alone does not prove the uninstaller
is signed. Install, upgrade and uninstall are tested with the exact signed
Setup; results from unsigned builds do not replace this step.

## Privacy

See the [privacy policy](../legal/PRIVACY.md). Tokens, private keys and logs
with user data never go into the repository or signed artifacts. Once approved,
SignPath attribution is added exactly as required; sponsorship is not claimed
before it exists.

Sources: [SignPath terms](https://signpath.org/terms.html),
[GitHub integration](https://docs.signpath.io/trusted-build-systems/github).
