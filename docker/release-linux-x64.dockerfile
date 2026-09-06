# Build environment for producing a Native AOT linux-x64 publish of lazynats.
# Not a runtime image (see default.dockerfile for that) - this only carries the SDK
# plus the toolchain Native AOT needs on Linux (clang, zlib1g-dev).

FROM mcr.microsoft.com/dotnet/sdk:10.0

RUN apt-get update && apt-get install -y --no-install-recommends clang zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*
