# Image of the containers of eng/ (see docs/design/eng-test.md). The build context is this directory:
# nothing of the working copy is copied into the image.

# The .NET 10 runtime is taken from the .NET 10 SDK image: tests and tools of net10.0 run on it.
FROM mcr.microsoft.com/dotnet/sdk:10.0.404@sha256:8c6edfb26c85153f236d26efebd9ad349aec403583f343f201ee67838b23658d AS runtime10

# The tag is the SDK version of global.json (11.0.100, the general availability release of .NET 11);
# it changes together with global.json. Floating tags are not used. Every image is also pinned by the digest after the tag (the
# OpenSSF Scorecard check Pinned-Dependencies); Dependabot (docker, /eng/docker) updates the tag and the digest together.
FROM mcr.microsoft.com/dotnet/sdk:11.0.100@sha256:2794448a07118a6eaf00f29ab5d2bf44928517ce5ff86505a63a69230374f599 AS sdk
COPY --from=runtime10 /usr/share/dotnet/shared/Microsoft.NETCore.App /usr/share/dotnet/shared/Microsoft.NETCore.App
# The ASP.NET Core shared framework 10.0: the tests of the ASP.NET Core package of net10.0 run on it.
COPY --from=runtime10 /usr/share/dotnet/shared/Microsoft.AspNetCore.App /usr/share/dotnet/shared/Microsoft.AspNetCore.App
# Files of the working copy and of the git directory belong to another host user: git must not refuse them.
RUN git config --system --add safe.directory /repo \
 && git config --system --add safe.directory /git

# The service docs runs eng/docs (docs/design/eng-docs.md): the sdk stage and the lychee program. The program is copied from the
# official image at its exact version, so the link checker is one program in the same container as eng/checks and not a container
# of its own. The tag changes together with the version of lychee that docs/design/eng-docs.md names.
FROM sdk AS docs
COPY --from=lycheeverse/lychee:0.24.2@sha256:e2d19e57cf6ab037026f20b8e449a1f30d9d7f81eef4194763aab2eab20bd28d /usr/local/bin/lychee /usr/local/bin/lychee

# The -aot variant of the same SDK image adds the native toolchain that Native AOT publishing needs; the rest is as in sdk.
# The service test runs on it (docs/design/eng-test.md). The tag changes together with global.json.
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-aot@sha256:b9ddcead074e2fc9338c3427f8f0e212e2cd4783ea26c8853ea412221518d1af AS sdk-aot
COPY --from=runtime10 /usr/share/dotnet/shared/Microsoft.NETCore.App /usr/share/dotnet/shared/Microsoft.NETCore.App
COPY --from=runtime10 /usr/share/dotnet/shared/Microsoft.AspNetCore.App /usr/share/dotnet/shared/Microsoft.AspNetCore.App
# Files of the working copy and of the git directory belong to another host user: git must not refuse them.
RUN git config --system --add safe.directory /repo  && git config --system --add safe.directory /git

# The oldest supported .NET 10 SDK, 10.0.404, the oldest that a user of the net10.0 package can have; it changes together with eng/min-sdk/global.json.
# The service min-sdk builds a sample application on it (docs/design/eng-test.md, "min-sdk").
FROM mcr.microsoft.com/dotnet/sdk:10.0.404@sha256:8c6edfb26c85153f236d26efebd9ad349aec403583f343f201ee67838b23658d AS sdk-min
# Files of the working copy and of the git directory belong to another host user: git must not refuse them. Only eng/run reads
# git here, for the commit and the state of the tree in its last line.
RUN git config --system --add safe.directory /repo \
 && git config --system --add safe.directory /git
