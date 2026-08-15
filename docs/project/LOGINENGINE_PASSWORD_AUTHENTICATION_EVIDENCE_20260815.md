# AORebirth LoginEngine Password Authentication Evidence

Status: Windows-authoritative password authentication repair. No production
database schema was changed, no production rows were changed, no website account
route was enabled, no MyBB installation was performed, and Linux production was
not touched.

## Root cause

Password validation regressed in commit `f7e9b657` (`Fixed LFT with filters`).
That commit removed the `UserCredentialsHandler` call to
`CheckLogin.IsLoginCorrect(...)` and rewrote `CheckLogin.IsLoginCorrect(...)`
to return `IsLoginAllowed(accountName)` without loading `login.Password` or
calling `LoginEncryption.IsValidLogin(...)`.

The earlier hardening commit `7fd60ad3` did not remove password validation. It
kept the challenge-generation boundary, restored `LoginEncryption.i_Enable =
true` for Debug and Release, and made disabled encryption fail closed instead
of accepting any password.

## Final authentication flow

```text
UserLoginHandler.Handle(UserLoginMessage)
  -> Client.BeginAuthentication(username, clientVersion, generatedServerSalt)
  -> ServerSaltMessage sent to official client

UserCredentialsHandler.Handle(UserCredentialsMessage)
  -> Client.TryBeginAuthenticationAttempt(credentials.Username)
  -> CheckLogin.IsLoginAllowed(challengedAccount)
       -> LoginName.GetLoginName(challengedAccount)
       -> LoginFlags.GetLoginFlags(challengedAccount)
       -> require login row and Flags == 0
  -> CheckLogin.IsLoginCorrect(challengedAccount, challengedServerSalt, credentials.Credentials)
       -> LoginPasswd.GetLoginPassword(challengedAccount)
       -> LoginEncryption.IsValidLogin(loginKey, serverSalt, accountName, storedHash)
            -> DecryptLoginKey(loginKey)
            -> require embedded username matches accountName
            -> require stored hash is present and parseable
            -> PasswordHash.ValidatePassword(embeddedPassword, storedHash)
            -> require embedded server salt matches challenged server salt
  -> LoginDataDao.GetByUsername(challengedAccount)
  -> CharacterList.LoadCharacters(authenticatedAccount)
  -> Client.CompleteAuthentication(authenticatedAccount, challengedGeneration)
  -> CharacterListMessage sent
```

Failure behavior remains protocol-stable: invalid username/account state,
incorrect password, malformed credential payload, and missing/malformed stored
hash all reject authentication through `Client.RejectAuthentication()` and
`LoginError.InvalidUserNamePassword`.

## Password contract

Generation:

```text
LoginEncryption.GeneratePasswordHash(clearPassword)
    -> PasswordHash.CreateHash(clearPassword)
```

Validation:

```text
LoginEncryption.IsValidLogin(loginKey, serverSalt, accountName, storedHash)
    -> PasswordHash.ValidatePassword(decryptedPassword, storedHash)
```

Stored format remains unchanged:

```text
iterations:base64(30-byte random salt):base64(30-byte PBKDF2-HMAC-SHA1 output)
```

No stored password hashes are rewritten during login.

## Fail-closed matrix

Validated by `Tools/LoginAuthenticationValidation`, which generates encrypted
AO-style login-key payloads and calls the LoginEngine `CheckLogin` credential
validation method.

| Case | Result |
| --- | --- |
| Correct password with generated stored hash | PASS |
| Incorrect password | PASS, rejected |
| Blank supplied password against nonblank hash | PASS, rejected |
| Case-different password | PASS, rejected |
| Special-character password | PASS |
| Long password | PASS |
| Blank generated password compatibility | PASS |
| Malformed stored hash | PASS, rejected |
| Empty stored hash / nonexistent-account equivalent | PASS, rejected |
| Username case variation when credential/account case match | PASS |
| Credential username mismatch | PASS, rejected |
| Server salt mismatch | PASS, rejected |
| Malformed credential payload | PASS, rejected |

`Flags` behavior remains unchanged: `CheckLogin` permits only `Flags == 0`.
Unsupported/nonzero `Flags` values remain rejected before password validation.

## Debug and Release behavior

`LoginEncryption.i_Enable` is now unconditional `true`; there is no current
`#if DEBUG` password-bypass branch in the inspected password-validation path.
Both Debug and Release builds execute the same password-validation semantics.

Validation results:

- Debug validation tool: PASS `14/14`.
- Release validation tool: PASS `14/14`.
- LoginEngine Debug build: PASS.
- LoginEngine Release build: PASS.
- Windows publish/production-equivalent: no LoginEngine Windows publish profile
  exists; the Release `LoginEngine.csproj` build is the applicable
  production-equivalent Windows artifact for this repository.

## Regression validation

- Database preflight wrapper: PASS.
- AOtomation messaging wrapper: PASS `1013/1013`.
- `git diff --check`: PASS.

## Boundary confirmation

- Production database rows changed: no.
- Production database schema changed: no.
- Proposed identity schema deployed: no.
- Website account routes changed/enabled: no.
- MyBB installed: no.
- Linux production touched: no.

## Remaining acceptance risk

The automated validation proves the restored LoginEngine `CheckLogin` password
verifier using real encrypted AO-style login-key payloads and generated
existing-format hashes. It does not perform a full official-client manual login
in this stage. Public Account Broker registration should not proceed until Mike
accepts this Windows evidence or performs the optional official-client manual
proof against the rebuilt LoginEngine.
