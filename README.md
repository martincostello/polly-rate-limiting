# Polly Rate-Limiting with ASP.NET Core

[![Build status](https://github.com/martincostello/polly-rate-limiting/workflows/build/badge.svg?branch=main&event=push)](https://github.com/martincostello/polly-rate-limiting/actions?query=workflow%3Abuild+branch%3Amain+event%3Apush)

[![Open in Visual Studio Code](https://open.vscode.dev/badges/open-in-vscode.svg)](https://open.vscode.dev/martincostello/polly-rate-limiting)

## Introduction

_TODO_

## Debugging

To debug the application locally outside of the integration tests, you will need
to [create a GitHub OAuth app] to obtain secrets for the `GitHub:ClientId` and
`GitHub:ClientSecret` options so that the [OAuth user authentication] works and
you can log into the Todo App UI.

> 💡 When creating the GitHub OAuth app, use `https://localhost:5001/sign-in-github`
as the _Authorization callback URL_.

> ⚠️ Do not commit GitHub OAuth secrets to source control. Configure them
with [User Secrets] instead.

[create a GitHub OAuth app]: https://docs.github.com/en/developers/apps/building-oauth-apps/creating-an-oauth-app
[OAuth user authentication]: https://docs.microsoft.com/en-us/aspnet/core/security/authentication/social/?view=aspnetcore-5.0&tabs=visual-studio
[User Secrets]: https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets

## Building and Testing

Compiling the application yourself requires Git and the
[.NET SDK](https://www.microsoft.com/net/download/core "Download the .NET SDK")
to be installed.

To build and test the application locally from a terminal/command-line, run the
following set of commands:

```powershell
git clone https://github.com/martincostello/polly-rate-limiting.git
cd polly-rate-limiting
./build.ps1
```

## Feedback

Any feedback or issues can be added to the issues for this project in
[GitHub](https://github.com/martincostello/polly-rate-limiting/issues "Issues for this project on GitHub.com").

## Repository

The repository is hosted in
[GitHub](https://github.com/martincostello/polly-rate-limiting "This project on GitHub.com"):
https://github.com/martincostello/polly-rate-limiting.git

## License

This project is licensed under the
[Apache 2.0](http://www.apache.org/licenses/LICENSE-2.0.txt "The Apache 2.0 license")
license.
