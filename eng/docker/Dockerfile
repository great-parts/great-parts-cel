# Image of the containers of eng/ (see docs/design/eng-test.md). The build context is this directory:
# nothing of the working copy is copied into the image.

# The .NET 10 runtime is taken from the .NET 10 SDK image: tests and tools of net10.0 run on it.
FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS runtime10

# The tag is the SDK version of global.json (11.0.100, the general availability release of .NET 11);
# it changes together with global.json. Floating tags are not used.
FROM mcr.microsoft.com/dotnet/sdk:11.0.100 AS sdk
COPY --from=runtime10 /usr/share/dotnet/shared/Microsoft.NETCore.App /usr/share/dotnet/shared/Microsoft.NETCore.App
# Files of the working copy and of the git directory belong to another host user: git must not refuse them.
RUN git config --system --add safe.directory /repo \
 && git config --system --add safe.directory /git

# The service docs runs eng/docs (docs/design/eng-docs.md): the sdk stage and the lychee program. The program is copied from the
# official image at its exact version, so the link checker is one program in the same container as eng/checks and not a container
# of its own. The tag changes together with the version of lychee that docs/design/eng-docs.md names.
FROM sdk AS docs
COPY --from=lycheeverse/lychee:0.24.2 /usr/local/bin/lychee /usr/local/bin/lychee

# The -aot variant of the same SDK image adds the native toolchain that Native AOT publishing needs; the rest is as in sdk.
# The service test runs on it (docs/design/eng-test.md). The tag changes together with global.json.
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-aot AS sdk-aot
COPY --from=runtime10 /usr/share/dotnet/shared/Microsoft.NETCore.App /usr/share/dotnet/shared/Microsoft.NETCore.App
# Files of the working copy and of the git directory belong to another host user: git must not refuse them.
RUN git config --system --add safe.directory /repo  && git config --system --add safe.directory /git

# The first .NET 10 SDK, the oldest that a user of the net10.0 package can have; it changes together with eng/min-sdk/global.json.
# The service min-sdk builds a sample application on it (docs/design/eng-test.md, "min-sdk").
FROM mcr.microsoft.com/dotnet/sdk:10.0.100 AS sdk-min
# Files of the working copy and of the git directory belong to another host user: git must not refuse them. Only eng/run reads
# git here, for the commit and the state of the tree in its last line.
RUN git config --system --add safe.directory /repo \
 && git config --system --add safe.directory /git
